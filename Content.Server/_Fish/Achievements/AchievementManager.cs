using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Fish.Achievements;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Achievements;

/// <summary>
/// Account-wide кеш, persistence и антиабуз. Выдача только с сервера по NetUserId.
/// </summary>
public sealed partial class AchievementManager : IPostInjectInit
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private UserDbDataManager _userDb = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEntitySystemManager _systems = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private ISawmill _sawmill = default!;
    private AchievementGameplayGateSystem? _gate;

    private readonly Dictionary<ICommonSession, Dictionary<string, AchievementPlayerState>> _cache = new();
    private readonly Dictionary<string, List<AchievementPrototype>> _byCondition = new();

    /// <summary>achievementId с реальной строкой в БД (загружено или upsert).</summary>
    private readonly Dictionary<NetUserId, HashSet<string>> _persistedAchievementIds = new();

    /// <summary>Upsert в БД за текущий раунд (per user).</summary>
    private readonly Dictionary<NetUserId, int> _dbUpsertsThisRound = new();

    /// <summary>Раундовые тики без EventKey: user → achievementId → roundSerial.</summary>
    private readonly Dictionary<NetUserId, Dictionary<string, int>> _roundProgressTicks = new();

    private readonly AchievementEventKeyTracker _eventKeys = new();

    private readonly Dictionary<(NetUserId User, string AchievementId), TimeSpan> _progressCooldownUntil = new();
    private readonly Dictionary<NetUserId, TimeSpan> _roundPresenceStart = new();

    /// <summary>Сериализация вкладов на пользователя (race / duplicate async).</summary>
    private readonly Dictionary<NetUserId, SemaphoreSlim> _userLocks = new();

    private int _roundSerial;
    private bool _conditionIndexReady;
    private AchievementGameplayGateSystem Gate => _gate ??= _systems.GetEntitySystem<AchievementGameplayGateSystem>();

    public event Action<ICommonSession, AchievementPlayerState, bool>? ProgressChanged;

    public void Initialize()
    {
        _sawmill = _log.GetSawmill("fish.achievements");
        // Не индексируем в Init — прототипы ещё не загружены (YAMLLinter / Map Renderer).
        _prototypes.PrototypesReloaded += _ => RebuildConditionIndex();
    }

    public void OnRoundStarting()
    {
        _roundSerial++;
        _roundProgressTicks.Clear();
        _eventKeys.Clear();
        _progressCooldownUntil.Clear();
        _roundPresenceStart.Clear();
        _dbUpsertsThisRound.Clear();
    }

    public void MarkRoundPresence(ICommonSession session)
    {
        _roundPresenceStart[session.UserId] = _timing.CurTime;
    }

    private void RebuildConditionIndex()
    {
        _byCondition.Clear();
        foreach (var proto in _prototypes.EnumeratePrototypes<AchievementPrototype>())
        {
            // manual — каталожные заглушки, event-handlers их не крутят.
            if (proto.Condition == AchievementConditionKeys.Manual)
                continue;

            if (!_byCondition.TryGetValue(proto.Condition, out var list))
            {
                list = [];
                _byCondition[proto.Condition] = list;
            }

            list.Add(proto);
        }

        _conditionIndexReady = true;
    }

    private void EnsureConditionIndex()
    {
        if (_conditionIndexReady)
            return;

        try
        {
            RebuildConditionIndex();
        }
        catch (InvalidOperationException)
        {
            // Прототипы ещё не загружены — следующий Contribute/reload подхватит.
        }
    }

    public bool TryGetState(ICommonSession session, out IReadOnlyDictionary<string, AchievementPlayerState> states)
    {
        if (_cache.TryGetValue(session, out var dict))
        {
            states = dict;
            return true;
        }

        states = new Dictionary<string, AchievementPlayerState>();
        return false;
    }

    public List<AchievementPlayerState> GetSnapshot(ICommonSession session)
    {
        if (!_cache.TryGetValue(session, out var dict))
            return [];

        return dict.Values.ToList();
    }

    public IReadOnlyList<AchievementPrototype> GetByCondition(string conditionKey)
    {
        EnsureConditionIndex();
        return _byCondition.TryGetValue(conditionKey, out var list) ? list : Array.Empty<AchievementPrototype>();
    }

    private SemaphoreSlim GetUserLock(NetUserId userId)
    {
        lock (_userLocks)
        {
            if (!_userLocks.TryGetValue(userId, out var sem))
            {
                sem = new SemaphoreSlim(1, 1);
                _userLocks[userId] = sem;
            }

            return sem;
        }
    }

    /// <summary>
    /// Вклад в семейство условий с антиабузом, EventKey-dedupe и индексацией по condition.
    /// </summary>
    public async Task ContributeAsync(
        ICommonSession session,
        string conditionKey,
        AchievementTriggerContext context = default,
        Func<AchievementPrototype, bool>? filter = null,
        int delta = 1)
    {
        if (delta <= 0 || !_cache.ContainsKey(session))
            return;

        // Дешёвые early-out до lock: duplicate EventKey / пустая семья условий.
        if (!string.IsNullOrEmpty(context.EventKey) &&
            _eventKeys.IsConsumed(session.UserId, context.EventKey))
            return;

        var candidates = GetByCondition(conditionKey);
        if (candidates.Count == 0)
            return;

        var sem = GetUserLock(session.UserId);
        await sem.WaitAsync();
        try
        {
            // Повторная проверка под lock (гонка двух Contribute с одним EventKey).
            if (!string.IsNullOrEmpty(context.EventKey) &&
                _eventKeys.IsConsumed(session.UserId, context.EventKey))
                return;

            var anyAccepted = false;

            foreach (var proto in candidates)
            {
                if (filter != null && !filter(proto))
                    continue;

                if (!AchievementAntiAbuseLogic.MatchesContext(proto, context))
                    continue;

                if (!Gate.CanEarnGameplay(session, proto, context.RequireInRound))
                    continue;

                bool ok;
                if (proto.ProgressTarget > 1)
                    ok = await TryAddProgressInternalAsync(session, proto, delta, context);
                else
                    ok = await TryUnlockInternalAsync(session, proto, context);

                anyAccepted |= ok;
            }

            if (anyAccepted && !string.IsNullOrEmpty(context.EventKey))
                _eventKeys.TryConsume(session.UserId, context.EventKey);
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task<bool> TryAddProgressAsync(ICommonSession session, string achievementId, int delta = 1)
    {
        if (!_prototypes.TryIndex<AchievementPrototype>(achievementId, out var proto))
            return false;

        var sem = GetUserLock(session.UserId);
        await sem.WaitAsync();
        try
        {
            return await TryAddProgressInternalAsync(session, proto, delta, default);
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task<bool> TryUnlockAsync(ICommonSession session, string achievementId)
    {
        if (!_prototypes.TryIndex<AchievementPrototype>(achievementId, out var proto))
            return false;

        var sem = GetUserLock(session.UserId);
        await sem.WaitAsync();
        try
        {
            return await TryUnlockInternalAsync(session, proto, default);
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// Принудительная выдача (admin), без gameplay-gate.
    /// </summary>
    public async Task<bool> TryForceUnlockAsync(ICommonSession session, string achievementId)
    {
        if (!_prototypes.TryIndex<AchievementPrototype>(achievementId, out var proto))
            return false;

        if (!_cache.TryGetValue(session, out var cache))
            return false;

        var sem = GetUserLock(session.UserId);
        await sem.WaitAsync();
        try
        {
            cache.TryGetValue(proto.ID, out var existing);
            if (existing.Unlocked)
                return false;

            var target = Math.Max(1, proto.ProgressTarget);
            return await CommitProgressAsync(session, cache, proto, existing, target, forcePersist: true);
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<bool> TryAddProgressInternalAsync(
        ICommonSession session,
        AchievementPrototype proto,
        int delta,
        AchievementTriggerContext context)
    {
        if (delta <= 0)
            return false;

        if (!_cache.TryGetValue(session, out var cache))
            return false;

        cache.TryGetValue(proto.ID, out var existing);
        if (existing.Unlocked)
            return false;

        if (!PassesAntiAbuse(session, proto, context))
            return false;

        var newProgress = existing.Progress + delta;
        var ok = await CommitProgressAsync(session, cache, proto, existing, newProgress);
        if (ok)
            OnSuccessfulProgress(session, proto, context);

        return ok;
    }

    private async Task<bool> TryUnlockInternalAsync(
        ICommonSession session,
        AchievementPrototype proto,
        AchievementTriggerContext context)
    {
        if (!_cache.TryGetValue(session, out var cache))
            return false;

        cache.TryGetValue(proto.ID, out var existing);
        if (existing.Unlocked)
            return false;

        if (!PassesAntiAbuse(session, proto, context))
            return false;

        var target = Math.Max(1, proto.ProgressTarget);
        var ok = await CommitProgressAsync(session, cache, proto, existing, target);
        if (ok)
            OnSuccessfulProgress(session, proto, context);

        return ok;
    }

    private bool PassesAntiAbuse(
        ICommonSession session,
        AchievementPrototype proto,
        AchievementTriggerContext context)
    {
        if (proto.MinRoundSeconds > 0)
        {
            if (!_roundPresenceStart.TryGetValue(session.UserId, out var start))
                return false;

            if ((_timing.CurTime - start).TotalSeconds < proto.MinRoundSeconds)
                return false;
        }

        // Без EventKey — once-per-round на ачивку (survive и т.п.).
        // С EventKey — разные события могут давать прогресс в одном раунде.
        if (string.IsNullOrEmpty(context.EventKey) &&
            proto.OncePerRound &&
            _roundProgressTicks.TryGetValue(session.UserId, out var map) &&
            map.TryGetValue(proto.ID, out var serial) &&
            serial == _roundSerial)
        {
            return false;
        }

        if (string.IsNullOrEmpty(context.EventKey))
        {
            var cooldownKey = (session.UserId, proto.ID);
            if (_progressCooldownUntil.TryGetValue(cooldownKey, out var until) && _timing.CurTime < until)
                return false;
        }

        return true;
    }

    private void OnSuccessfulProgress(
        ICommonSession session,
        AchievementPrototype proto,
        AchievementTriggerContext context)
    {
        if (string.IsNullOrEmpty(context.EventKey) && proto.OncePerRound)
        {
            if (!_roundProgressTicks.TryGetValue(session.UserId, out var map))
            {
                map = new Dictionary<string, int>();
                _roundProgressTicks[session.UserId] = map;
            }

            map[proto.ID] = _roundSerial;
        }

        if (string.IsNullOrEmpty(context.EventKey))
        {
            var cooldown = TimeSpan.FromSeconds(Math.Max(0, proto.ProgressCooldownSeconds));
            if (cooldown > TimeSpan.Zero)
                _progressCooldownUntil[(session.UserId, proto.ID)] = _timing.CurTime + cooldown;
        }
    }

    private async Task<bool> CommitProgressAsync(
        ICommonSession session,
        Dictionary<string, AchievementPlayerState> cache,
        AchievementPrototype proto,
        AchievementPlayerState existing,
        int newProgress,
        bool forcePersist = false)
    {
        // Уже unlocked — никаких повторных write/notify.
        if (existing.Unlocked)
            return false;

        var target = Math.Max(1, proto.ProgressTarget);
        var wouldUnlock = newProgress >= target;
        var hasDbRow = HasPersistedRow(session.UserId, proto.ID);

        if (!forcePersist)
        {
            if (!Gate.CanPersistToDatabase(session))
                return ApplyMemoryProgress(session, cache, proto, existing, newProgress);

            if (_cfg.GetCVar(FishCVars.AchievementsPersistOnlyOnUnlock) && !wouldUnlock && !hasDbRow)
                return ApplyMemoryProgress(session, cache, proto, existing, newProgress);

            if (!TryConsumeDbUpsertBudget(session.UserId))
            {
                _sawmill.Info(
                    "Achievement DB budget exceeded for {User}, achievement {Id} kept in RAM",
                    session.UserId,
                    proto.ID);
                return ApplyMemoryProgress(session, cache, proto, existing, newProgress);
            }
        }

        var entry = await _db.UpsertFishAchievementProgressAsync(
            session.UserId.UserId,
            proto.ID,
            newProgress,
            proto.ProgressTarget);

        MarkPersisted(session.UserId, proto.ID);

        var state = ToState(entry);
        cache[proto.ID] = state;
        var justUnlocked = state.Unlocked && !existing.Unlocked;
        var progressChanged = state.Progress != existing.Progress;

        if (!justUnlocked && !progressChanged)
            return false;

        ProgressChanged?.Invoke(session, state, justUnlocked);
        return true;
    }

    /// <summary>
    /// Прогресс только в серверном RAM — без строки в БД (сессия / reconnect теряет partial).
    /// </summary>
    private bool ApplyMemoryProgress(
        ICommonSession session,
        Dictionary<string, AchievementPlayerState> cache,
        AchievementPrototype proto,
        AchievementPlayerState existing,
        int newProgress)
    {
        var target = Math.Max(1, proto.ProgressTarget);
        var clamped = Math.Clamp(newProgress, 0, target);
        if (clamped == existing.Progress)
            return false;

        var wouldUnlock = clamped >= target;
        // Тот же Overall playtime gate, что и для БД — без unlock в RAM для новорегов/ботов.
        if (wouldUnlock && !Gate.CanPersistToDatabase(session))
            clamped = target - 1;

        var unlocked = clamped >= target;
        var state = new AchievementPlayerState(
            proto.ID,
            clamped,
            unlocked,
            unlocked ? _timing.CurTime : null);

        cache[proto.ID] = state;
        var justUnlocked = unlocked && !existing.Unlocked;
        ProgressChanged?.Invoke(session, state, justUnlocked);
        return true;
    }

    private bool HasPersistedRow(NetUserId user, string achievementId)
    {
        return _persistedAchievementIds.TryGetValue(user, out var set) && set.Contains(achievementId);
    }

    private void MarkPersisted(NetUserId user, string achievementId)
    {
        if (!_persistedAchievementIds.TryGetValue(user, out var set))
        {
            set = new HashSet<string>();
            _persistedAchievementIds[user] = set;
        }

        set.Add(achievementId);
    }

    private bool TryConsumeDbUpsertBudget(NetUserId user)
    {
        var max = _cfg.GetCVar(FishCVars.AchievementsMaxDbUpsertsPerRound);
        if (max <= 0)
            return true;

        _dbUpsertsThisRound.TryGetValue(user, out var count);
        if (count >= max)
            return false;

        _dbUpsertsThisRound[user] = count + 1;
        return true;
    }

    private async Task LoadData(ICommonSession session, CancellationToken cancel)
    {
        var rows = await _db.GetFishAchievementsAsync(session.UserId.UserId, cancel);
        var dict = new Dictionary<string, AchievementPlayerState>(rows.Count);
        var persisted = new HashSet<string>(rows.Count);
        foreach (var row in rows)
        {
            dict[row.AchievementId] = ToState(row);
            persisted.Add(row.AchievementId);
        }

        _cache[session] = dict;
        _persistedAchievementIds[session.UserId] = persisted;
    }

    private void ClientDisconnected(ICommonSession session)
    {
        _cache.Remove(session);
        // Round ticks / EventKeys / cooldown по UserId сохраняем — reconnect в том же раунде не сбрасывает антиабуз.
        _roundPresenceStart.Remove(session.UserId);
    }

    /// <summary>
    /// Сбрасывает RAM-состояние достижений аккаунта (после purge в БД).
    /// </summary>
    public void PurgePlayer(NetUserId userId, ICommonSession? session = null)
    {
        if (session != null)
        {
            _cache.Remove(session);
        }
        else
        {
            foreach (var cachedSession in _cache.Keys.ToList())
            {
                if (cachedSession.UserId == userId)
                    _cache.Remove(cachedSession);
            }
        }

        _persistedAchievementIds.Remove(userId);
        _dbUpsertsThisRound.Remove(userId);
        _roundProgressTicks.Remove(userId);
        _roundPresenceStart.Remove(userId);
        _eventKeys.ClearUser(userId);

        foreach (var key in _progressCooldownUntil.Keys.ToList())
        {
            if (key.User == userId)
                _progressCooldownUntil.Remove(key);
        }

        lock (_userLocks)
        {
            _userLocks.Remove(userId);
        }
    }

    private static AchievementPlayerState ToState(FishAchievementProgress entry)
    {
        TimeSpan? unlockedAt = entry.UnlockedAt is { } at
            ? at.UtcDateTime - DateTime.UnixEpoch
            : null;

        return new AchievementPlayerState(
            entry.AchievementId,
            entry.Progress,
            entry.UnlockedAt != null,
            unlockedAt);
    }

    void IPostInjectInit.PostInject()
    {
        _userDb.AddOnLoadPlayer(LoadData);
        _userDb.AddOnPlayerDisconnect(ClientDisconnected);
    }
}
