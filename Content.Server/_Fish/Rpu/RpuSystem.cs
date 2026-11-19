using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._Fish.DmArena;
using Content.Shared.Players.PlayTimeTracking;
using Content.Server.Shuttles.Components;
using Content.Shared._Fish.Rpu;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.CCVar;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Ядро режима персонального угнетения: карма, пороги, логи, автобан.
/// Работает только на сервере и только по событиям действий, без опроса всех игроков.
/// </summary>
public sealed partial class RpuSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IBanManager _bans = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IAdminLogManager _adminLogs = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private UserDbDataManager _userDb = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedTransformSystem _transforms = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;
    [Dependency] private RpuModifierSystem _modifiers = default!;

    private readonly Dictionary<NetUserId, RpuSuspect> _suspects = new();

    private ISawmill _sawmill = default!;

    private bool _enabled;
    private RpuAdminMode _adminMode;
    private readonly HashSet<string> _exemptRanks = new();
    private HashSet<string> _roleTrackers = new();

    public bool RoundActive { get; private set; }

    /// <summary>Эвакуация пристыковалась: RDM по правилам разрешён, систему глушим.</summary>
    public bool EvacuationDocked { get; private set; }

    public bool SystemEnabled => _enabled;

    /// <summary>Граница «новичка» в часах игры на должностях (cvar).</summary>
    public float NewbieBoundaryHours => _cfg.GetCVar(RpuCVars.NewbieHours);

    /// <summary>Сколько контрабандных предметов считается избыточным.</summary>
    public int ContrabandLimit => Math.Max(1, _cfg.GetCVar(RpuCVars.ContrabandItemLimit));

    public float ContrabandDwellMinutes => _cfg.GetCVar(RpuCVars.ContrabandDwellMinutes);

    public FixedPoint2 SpillThreshold => FixedPoint2.New(_cfg.GetCVar(RpuCVars.SpillUnitThreshold));

    public int SingularityWatchTiles => Math.Max(1, _cfg.GetCVar(RpuCVars.SingularityWatchTiles));

    public float SingularityWatchMinutes => _cfg.GetCVar(RpuCVars.SingularityWatchMinutes);

    public bool TryGetSession(NetUserId userId, out ICommonSession session)
    {
        return _players.SessionsDict.TryGetValue(userId, out session!);
    }

    /// <summary>Раунд идёт и система не заглушена (эвакуация, режим, отключение).</summary>
    public bool Active =>
        _enabled
        && _ticker.RunLevel is GameRunLevel.InRound or GameRunLevel.PostRound
        && !EvacuationDocked
        && !PresetDisabled
        && !ArenaRunning;

    /// <summary>
    /// Идёт ли сейчас матч арены (Deathmatch-аrena): там убийства — это сама игра.
    /// Обновляется раз в минуту вместе с восстановлением кармы.
    /// </summary>
    public bool ArenaRunning { get; private set; }

    private bool PresetDisabled
    {
        get
        {
            if (_tickersPresetDisabledCache.HasValue)
                return _tickersPresetDisabledCache.Value;

            var preset = _ticker.CurrentPreset?.ID;
            var disabled = false;
            if (preset != null)
                disabled = Tuning.DisabledGamePresets.Contains(preset);

            _tickersPresetDisabledCache = disabled;
            return disabled;
        }
    }

    private bool? _tickersPresetDisabledCache;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("rpu");

        Subs.CVar(_cfg, RpuCVars.Enabled, value => _enabled = value, true);
        Subs.CVar(_cfg, RpuCVars.AdminMode, value => ParseAdminMode(value), true);
        Subs.CVar(_cfg, RpuCVars.ExemptRanks, value => ParseExemptRanks(value), true);

        _userDb.AddOnLoadPlayer(LoadProfile);
        _userDb.AddOnPlayerDisconnect(OnDisconnect);

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<Content.Server.Shuttles.Events.DockEvent>(OnDock);

        BuildRoleTrackerSet();
    }

    private void ParseAdminMode(string value)
    {
        _adminMode = value.Trim().ToLowerInvariant() switch
        {
            "off" => RpuAdminMode.Off,
            "normal" => RpuAdminMode.Normal,
            _ => RpuAdminMode.LogOnly,
        };
    }

    private void ParseExemptRanks(string value)
    {
        _exemptRanks.Clear();
        foreach (var rank in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            _exemptRanks.Add(rank.ToLowerInvariant());
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<PlayTimeTrackerPrototype>() || args.WasModified<JobPrototype>() || args.WasModified<AntagPrototype>())
            BuildRoleTrackerSet();

        _tickersPresetDisabledCache = null;
    }

    /// <summary>Треки наигранного времени, которые считаются «игрой на роли».</summary>
    private void BuildRoleTrackerSet()
    {
        var set = new HashSet<string>();

        foreach (var job in _prototypes.EnumeratePrototypes<JobPrototype>())
            set.Add(job.PlayTimeTracker);

        foreach (var antag in _prototypes.EnumeratePrototypes<AntagPrototype>())
            set.Add(antag.PlayTimeTracker);

        _roleTrackers = set;
    }

    private async Task LoadProfile(ICommonSession session, CancellationToken cancel)
    {
        var suspect = GetSuspect(session.UserId);

        suspect.RoleHours = await Task.Run(() => GetRoleHours(session));
        suspect.BaseKarma = RpuMaths.ClampKarma(
            (float)(suspect.RoleHours * _cfg.GetCVar(RpuCVars.BaseKarmaPerHour)));

        try
        {
            var stored = await _db.GetFishRpuKarmaAsync(session.UserId.UserId);
            suspect.Karma = stored ?? 0f;
            suspect.RecentBans = await CountRecentBans(session.UserId);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to load karma profile for {session.Name}: {e}");
        }

        var tuning = Tuning;
        {
            var (multiplier, shift) = RpuMaths.BanHistoryModifiers(suspect.RecentBans, tuning.BanHistoryTiers);
            suspect.BanHistoryMultiplier = multiplier;
            suspect.BanThresholdShift = shift;
        }

        // Положительная «база» позволяет ветеранам копить запас кармы.
        if (suspect.Karma < suspect.BaseKarma)
            suspect.Karma = Math.Min(suspect.BaseKarma, RpuMaths.ClampKarma(suspect.Karma));

        if (suspect.Karma > suspect.BaseKarma)
            suspect.Karma = suspect.BaseKarma;
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        EvacuationDocked = false;
        _tickersPresetDisabledCache = null;
        ArenaRunning = EntityQuery<DmArenaRuleComponent>().Any();

        foreach (var suspect in _suspects.Values)
        {
            suspect.KillsThisRound = 0;
            suspect.ChatLogSent = false;
            suspect.CleanHoursAccumulated = 0f;
        }
    }

    /// <summary>Пристыкованная эвакуация — RDM разрешён, систему глушим до конца раунда.</summary>
    public void NotifyEvacuationDocked()
    {
        if (EvacuationDocked)
            return;

        EvacuationDocked = true;
        _sawmill.Info("Evacuation shuttle docked: karma accounting suspended until the round ends.");
    }

    private void OnDock(Content.Server.Shuttles.Events.DockEvent args)
    {
        if (EvacuationDocked)
            return;

        if (HasComp<EmergencyShuttleComponent>(args.GridAUid) || HasComp<EmergencyShuttleComponent>(args.GridBUid))
            NotifyEvacuationDocked();
    }

    private void OnDisconnect(ICommonSession session)
    {
        if (_suspects.Remove(session.UserId, out var suspect))
            SaveKarma(suspect, session.UserId);
    }

    private double GetRoleHours(ICommonSession session)
    {
        if (!_playTime.TryGetTrackerTimes(session, out var times))
            return 0;

        var total = TimeSpan.Zero;
        foreach (var (tracker, value) in times)
        {
            if (_roleTrackers.Contains(tracker))
                total += value;
        }

        return total.TotalHours;
    }

    private async Task<int> CountRecentBans(NetUserId userId)
    {
        var tuning = Tuning;
        var days = tuning.BanHistoryDays;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        var bans = await _db.GetBansAsync(null, userId, null, null, true, BanType.Server);
        return bans.Count(ban => ban.BanTime >= cutoff);
    }

    public RpuSuspect GetSuspect(NetUserId userId)
    {
        if (!_suspects.TryGetValue(userId, out var suspect))
        {
            suspect = new RpuSuspect();
            _suspects[userId] = suspect;
        }

        return suspect;
    }

    public bool TryGetSuspect(NetUserId userId, out RpuSuspect suspect)
    {
        return _suspects.TryGetValue(userId, out suspect!);
    }

    public IEnumerable<KeyValuePair<NetUserId, RpuSuspect>> Suspects => _suspects;

    /// <summary>Не подвергается ли игрок системе (ранг, админ-режим).</summary>
    public bool IsExempt(ICommonSession session)
    {
        var admin = _admins.GetAdminData(session);
        if (admin == null)
            return false;

        // В deadmin игрок считается обычным пользователем.
        if (!admin.Active)
            return false;

        if (admin.Title != null && _exemptRanks.Contains(admin.Title.Trim().ToLowerInvariant()))
            return true;

        return _adminMode == RpuAdminMode.Off;
    }

    /// <summary>Действуют ли на игрока «тихие» эффекты и автобан.</summary>
    public bool PunishmentAllowed(ICommonSession session)
    {
        if (!Active || IsExempt(session))
            return false;

        // Для действующих админов в режиме log_only только учёт.
        return _admins.GetAdminData(session) == null || _adminMode != RpuAdminMode.LogOnly;
    }

    /// <summary>
    /// Единственная точка начисления штрафа. Ничего не делает, если система выключена,
    /// раунда нет, игрок освобождён или нарушение полностью перекрыто целью/ролью.
    /// </summary>
    public void Penalize(ICommonSession? session,
        ProtoId<RpuViolationPrototype> violation,
        EntityUid? victim = null,
        float severity = 1f,
        string? details = null)
    {
        if (session == null || !Active)
            return;

        if (IsExempt(session))
            return;

        if (!TryGetViolation(violation, out var proto))
        {
            _sawmill.Error($"Unknown RPU violation prototype {violation}");
            return;
        }

        var suspect = GetSuspect(session.UserId);

        if (_modifiers.IsFullyExempt(session, suspect, proto.Category, victim))
            return;

        var modifiers = _modifiers.CollectModifiers(session, suspect, proto.Category, victim);
        var multiplier = RpuMaths.CombineMultipliers(modifiers,
            _cfg.GetCVar(RpuCVars.ModifierWeightDecay),
            _cfg.GetCVar(RpuCVars.ModifierFloor),
            _cfg.GetCVar(RpuCVars.ModifierCap));

        var penalty = Math.Max(0.5f, proto.Base * severity * multiplier);
        var before = suspect.Karma;
        suspect.Karma = RpuMaths.ClampKarma(suspect.Karma - penalty);
        suspect.MarkViolation(_timing.CurTime);
        suspect.Dirty = true;

        var actual = before - suspect.Karma;
        if (actual <= 0f)
            return;

        LogKarmaChange(session, -actual, proto.Category.ToString());
        CheckChatThreshold(session, suspect, before, actual);
        CheckAutoBan(session, suspect);
    }

    /// <summary>Начисление кармы (восстановление, отмена ошибочного штрафа).</summary>
    public void AwardKarma(ICommonSession? session, float amount, RpuCategory category, string? details = null)
    {
        if (session == null || amount <= 0f)
            return;

        var suspect = GetSuspect(session.UserId);
        var before = suspect.Karma;
        suspect.Karma = RpuMaths.ClampKarma(suspect.Karma + amount);

        var actual = suspect.Karma - before;
        if (actual <= 0f)
            return;

        suspect.Dirty = true;
        LogKarmaChange(session, actual, details ?? "adjustment");
    }

    private bool TryGetViolation(ProtoId<RpuViolationPrototype> id, out RpuViolationPrototype proto)
    {
        return _prototypes.TryIndex(id, out proto!);
    }

    /// <summary>
    /// Прямая правка кармы администратором: без проверки освобождений и множителей.
    /// </summary>
    public bool TryAdjustKarma(ICommonSession session, float delta)
    {
        if (Math.Abs(delta) < 0.001f)
            return true;

        var suspect = GetSuspect(session.UserId);
        var before = suspect.Karma;
        suspect.Karma = RpuMaths.ClampKarma(suspect.Karma + delta);

        var actual = suspect.Karma - before;
        if (Math.Abs(actual) < 0.001f)
            return false;

        suspect.Dirty = true;
        LogKarmaChange(session, actual, "admin adjustment");
        return true;
    }

    private float _recoverAccumulator;
    private float _arenaAccumulator;
    private float _saveAccumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled)
            return;

        _arenaAccumulator += frameTime;
        if (_arenaAccumulator >= 5f)
        {
            _arenaAccumulator = 0f;
            ArenaRunning = EntityQuery<DmArenaRuleComponent>().Any();
        }

        _recoverAccumulator += frameTime;
        if (_recoverAccumulator >= 60f)
        {
            var hours = _recoverAccumulator / 3600f;
            _recoverAccumulator = 0f;

            if (Active)
                RecoverHourly(hours);
        }

        _saveAccumulator += frameTime;
        if (_saveAccumulator < 30f)
            return;

        _saveAccumulator = 0f;

        foreach (var (userId, suspect) in _suspects)
        {
            if (!suspect.Dirty)
                continue;

            suspect.Dirty = false;
            SaveKarma(suspect, userId);
        }
    }

    /// <summary>
    /// Восстановление: 0.25 кармы в час раунда, если за этот час система не сработала.
    /// Выше накопленной «базы» по наигранному времени не поднимаемся.
    /// </summary>
    private void RecoverHourly(float hours)
    {
        var rate = _cfg.GetCVar(RpuCVars.RecoveryPerHour) * hours;
        if (rate <= 0f)
            return;

        foreach (var (userId, suspect) in _suspects)
        {
            if (suspect.TriggeredThisHour)
            {
                suspect.TriggeredThisHour = false;
                continue;
            }

            if (!_players.SessionsDict.TryGetValue(userId, out var session) || session.Status != SessionStatus.InGame)
                continue;

            if (IsExempt(session) || suspect.Karma >= suspect.BaseKarma)
                continue;

            var ceiling = Math.Min(RpuMaths.KarmaMax, suspect.BaseKarma);
            var before = suspect.Karma;
            suspect.Karma = Math.Min(ceiling, suspect.Karma + rate);

            var gained = suspect.Karma - before;
            if (gained <= 0f)
                continue;

            suspect.RecoveryPending += gained;

            // Дробные шаги в лог не просим: пишем, когда накопилось хотя бы 1.
            if (suspect.RecoveryPending < 1f)
                continue;

            var amount = suspect.RecoveryPending;
            suspect.RecoveryPending = 0f;
            suspect.Dirty = true;
            LogKarmaChange(session, amount, "passive recovery");
        }
    }

    private async void SaveKarma(RpuSuspect suspect, NetUserId userId)
    {
        try
        {
            await _db.SetFishRpuKarmaAsync(userId.UserId, suspect.Karma);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to save karma for {userId}: {e}");
        }
    }

    private RpuTuningPrototype? _tuning;

    /// <summary>Служебные таблицы системы. Если прототип не найден — возвращаем безопасный дефолт.</summary>
    public RpuTuningPrototype Tuning
    {
        get
        {
            if (_tuning != null)
                return _tuning;

            if (!_prototypes.TryIndex(RpuPrototypeIds.Tuning, out var found))
            {
                _sawmill.Error($"Missing prototype {RpuPrototypeIds.Tuning}; RPU falls back to safe defaults.");
#pragma warning disable RA0039 // Безопасный дефолт на случай отсутствующего YAML.
                _tuning = new RpuTuningPrototype();
#pragma warning restore RA0039
                return _tuning;
            }

            _tuning = found;
            return _tuning;
        }
    }
}
