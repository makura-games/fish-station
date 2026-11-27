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

using Robust.Shared.Enums;
using Robust.Shared.Audio.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Gibbing;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Throwing;
using Content.Shared.Wires;
using Content.Shared.Slippery;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Random;
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
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;
    [Dependency] private RpuAdjustSystem _adjust = default!;

    private readonly Dictionary<NetUserId, RpuSuspect> _suspects = new();

    private ISawmill _sawmill = default!;

    private bool _enabled;
    private RpuAdminMode _adminMode;
    private readonly HashSet<string> _exemptRanks = new();
    private HashSet<string> _roleTrackers = new();
    private RpuTuningPrototype? _tuning;

    // Рамки снятия очков. Держим в полях, а не читаем cvar на каждое нарушение,
    // и чистим значения: кривой конфиг не должен уронить раунд.
    private float _minPenalty = RpuMaths.MinPenalty;

    /// <summary>Сколько очков потери за окно считается всплеском. 0 — правило выключено.</summary>
    private float _spikeUnits = 70f;

    /// <summary>Окно, за которое смотрим на потерю кармы.</summary>
    private TimeSpan _spikeWindow = TimeSpan.FromMinutes(10);
    private float _maxExtraUnits = RpuMaths.MaxExtraPenalty;

    private float _recoverAccumulator;
    private float _arenaAccumulator;
    private float _saveAccumulator;

    private static readonly TimeSpan SaveRetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>Сколько часов раунда должно пройти без нарушений, прежде чем карма начнёт оттаивать.</summary>
    private static readonly TimeSpan RecoveryQuietPeriod = TimeSpan.FromHours(1);

    private const float ArenaCheckSeconds = 5f;
    private const float SaveCheckSeconds = 30f;
    private const float RecoveryCheckSeconds = 60f;

    public bool RoundActive { get; private set; }

    /// <summary>Эвакуация пристыковалась: RDM по правилам разрешён, систему глушим.</summary>
    public bool EvacuationDocked { get; private set; }

    public bool SystemEnabled => _enabled;

    /// <summary>Граница «новичка» в часах игры на должностях (cvar).</summary>
    public float NewbieBoundaryHours => _cfg.GetCVar(FishCVars.RpuNewbieHours);

    /// <summary>Сколько контрабандных предметов считается избыточным.</summary>
    public int ContrabandLimit => Math.Max(1, _cfg.GetCVar(FishCVars.RpuContrabandItemLimit));

    public float ContrabandDwellMinutes => _cfg.GetCVar(FishCVars.RpuContrabandDwellMinutes);

    public FixedPoint2 SpillThreshold => FixedPoint2.New(_cfg.GetCVar(FishCVars.RpuSpillUnitThreshold));

    public int SingularityWatchTiles => Math.Max(1, _cfg.GetCVar(FishCVars.RpuSingularityWatchTiles));

    public float SingularityWatchMinutes => _cfg.GetCVar(FishCVars.RpuSingularityWatchMinutes);

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
    /// Правило живёт до конца раунда, поэтому смотрим на признак активного матча,
    /// а не на наличие компонента. Обновляется раз в минуту.
    /// </summary>
    public bool ArenaRunning { get; private set; }

    private bool IsArenaMatchRunning()
    {
        var query = AllEntityQuery<DmArenaRuleComponent>();
        while (query.MoveNext(out _, out var rule))
        {
            if (rule.Active)
                return true;
        }

        return false;
    }

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

        Subs.CVar(_cfg, FishCVars.RpuEnabled, value => _enabled = value, true);
        Subs.CVar(_cfg, FishCVars.RpuAdminMode, value => ParseAdminMode(value), true);
        Subs.CVar(_cfg, FishCVars.RpuExemptRanks, value => ParseExemptRanks(value), true);
        // Минимум обязан быть положительным: при нуле вменяемое нарушение с выгодной
        // поправкой свернулось бы в ноль и осталось бы без записи.
        Subs.CVar(_cfg, FishCVars.RpuMinPenalty,
            value => _minPenalty = SanitizeUnits(value, RpuMaths.MinPenalty, 25f, true), true);
        Subs.CVar(_cfg, FishCVars.RpuMaxExtraUnits,
            value => _maxExtraUnits = SanitizeUnits(value, RpuMaths.MaxExtraPenalty, 100f), true);
        Subs.CVar(_cfg, FishCVars.RpuSpikeUnits,
            value => _spikeUnits = float.IsFinite(value) && value >= 0f ? value : 70f, true);
        Subs.CVar(_cfg, FishCVars.RpuSpikeMinutes,
            value => _spikeWindow = TimeSpan.FromMinutes(value > 0 && value <= 240 ? value : 10), true);

        _userDb.AddOnLoadPlayer(LoadProfile);
        // Часы на должностях берём после загрузки трекеров: обработчики загрузки идут
        // параллельно, и во время нашего чтения трекеры могли быть ещё пусты.
        _userDb.AddOnFinishLoad(OnTrackersReady);
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

    /// <summary>
    /// Рамки снятия очков: без NaN, без отрицательных, в разумном диапазоне.
    /// Кривое значение заменяем безопасным и ругаемся в лог.
    /// </summary>
    private float SanitizeUnits(float value, float fallback, float max, bool requirePositive = false)
    {
        if (!float.IsFinite(value) || value < 0f || (requirePositive && value <= 0f))
        {
            _sawmill.Error($"Invalid RPU penalty value {value}, falling back to {fallback}.");
            return fallback;
        }

        return Math.Min(value, max);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<PlayTimeTrackerPrototype>() || args.WasModified<JobPrototype>() || args.WasModified<AntagPrototype>())
            BuildRoleTrackerSet();

        // После перезагрузки прототипов движок отдаёт новый экземпляр настройки: кеш обязан протухнуть.
        if (args.WasModified<RpuTuningPrototype>())
            _tuning = null;

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

        // Чтение уже идёт: подписка AddOnLoadPlayer и повтор по таймеру могут совпасть.
        // Второе чтение, завершившись позже первого, записало бы в карму значение из базы
        // и потеряло штраф, начисленный между ними.
        if (suspect.LoadInFlight)
            return;

        // Память новее базы, если запись об этом игроке ещё не подтверждена: быстрое
        // переподключение не должно стирать уже начисленный штраф.
        var keepMemory = suspect.Dirty || suspect.SaveInFlight;

        // Пока карму не прочитали, с ней ничего не делаем: иначе выход игрока
        // запишет в базу ноль вместо настоящих значений.
        if (!keepMemory)
            suspect.KarmaLoaded = false;

        suspect.Offline = false;
        suspect.LoadInFlight = true;

        try
        {
            var stored = await _db.GetFishRpuKarmaAsync(session.UserId.UserId);
            var bans = await CountRecentBans(session.UserId);

            if (cancel.IsCancellationRequested)
                return;

            if (!keepMemory)
                suspect.Karma = RpuMaths.ClampKarma(stored ?? 0f);

            suspect.RecentBans = bans;

            var (units, shift) = RpuMaths.BanHistory(bans, Tuning.BanHistoryTiers);
            suspect.BanHistoryUnits = units;
            suspect.BanThresholdShift = shift;

            // Сохранённую карму «базой» не обрезаем: правка администратора должна пережить
            // переподключение. BaseKarma остаётся только потолком пассивного восстановления.
            suspect.KarmaLoaded = true;
            suspect.LoadRetryAt = TimeSpan.Zero;
        }
        catch (Exception e)
        {
            // Временный сбой БД не должен отключать учёт на всю сессию: Update
            // повторит чтение через паузу.
            _sawmill.Error($"Failed to load karma profile for {session.Name}: {e}. Will retry.");
            suspect.LoadRetryAt = _timing.CurTime + LoadRetryDelay;
        }
        finally
        {
            suspect.LoadInFlight = false;
        }
    }

    /// <summary>Пауза перед повторным чтением профиля после сбоя БД.</summary>
    private static readonly TimeSpan LoadRetryDelay = TimeSpan.FromSeconds(30);

    private float _loadAccumulator;

    /// <summary>
    /// Трекеры наигранного времени готовы: считаем часы на должностях и «базу» кармы.
    /// </summary>
    private void OnTrackersReady(ICommonSession session)
    {
        if (!_suspects.TryGetValue(session.UserId, out var suspect))
            return;

        RefreshPlaytime(session, suspect);
    }

    /// <summary>
    /// Пересчитывает наигранное время и потолок восстановления. Нужно и в середине
    /// сессии: вошедший с 4,9 часа игрок за раунд переползает через ступень порогов.
    /// </summary>
    private void RefreshPlaytime(ICommonSession session, RpuSuspect suspect)
    {
        if (!_playTime.TryGetTrackerTimes(session, out var times) || times.Count == 0)
            return;

        suspect.RoleHours = GetRoleHours(session);
        suspect.BaseKarma = RpuMaths.ClampKarma(
            (float)(suspect.RoleHours * _cfg.GetCVar(FishCVars.RpuBaseKarmaPerHour)));
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        EvacuationDocked = false;
        _tickersPresetDisabledCache = null;
        ArenaRunning = IsArenaMatchRunning();

        foreach (var suspect in _suspects.Values)
        {
            suspect.KillsThisRound = 0;
            suspect.ChatLogSent = false;
            suspect.RecoveryPending = 0f;
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
        // Сессия закончилась: отметка о выданном бане больше не нужна, а при
        // переподключении порог должен сработать заново.
        ForgetBannedSession(session);

        if (!_suspects.TryGetValue(session.UserId, out var suspect))
            return;

        suspect.Offline = true;

        // Не прочитали — не пишем: есть риск стереть настоящую карму нулём.
        if (!suspect.KarmaLoaded)
        {
            _suspects.Remove(session.UserId);
            return;
        }

        // Профиль держим, пока запись не подтверждена базой: она либо ещё в полёте,
        // либо помечена на повтор. Иначе FinishSave не найдёт профиль и потеряет изменение.
        if (!suspect.Dirty && !suspect.SaveInFlight)
        {
            _suspects.Remove(session.UserId);
            return;
        }

        // Запись уйдёт через общий механизм с подтверждением; профиль держим в памяти,
        // пока база не ответит успехом (см. FinishSave).
        SaveKarma(suspect, session.UserId);
    }

    private double GetRoleHours(ICommonSession session)
    {
        if (!_playTime.TryGetTrackerTimes(session, out var times))
        {
            _sawmill.Warning($"Playtime trackers are unavailable for {session.Name}; role hours count as zero.");
            return 0;
        }

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

    /// <summary>
    /// В админке ли игрок прямо сейчас. Deadmin-игрок — обычный пользователь,
    /// и система обращается с ним так же, как со всеми.
    /// </summary>
    private bool IsActiveAdmin(ICommonSession session)
    {
        return _admins.GetAdminData(session) is { Active: true };
    }

    /// <summary>Ранг игрока в списке освобождённых от системы.</summary>
    private bool HasExemptRank(ICommonSession session)
    {
        var title = _admins.GetAdminData(session)?.Title;
        return title != null && _exemptRanks.Contains(title.Trim().ToLowerInvariant());
    }

    /// <summary>Не подвергается ли игрок системе (ранг, админ-режим).</summary>
    public bool IsExempt(ICommonSession session)
    {
        if (!IsActiveAdmin(session))
            return false;

        if (HasExemptRank(session))
            return true;

        return _adminMode == RpuAdminMode.Off;
    }

    /// <summary>Разрешено ли отключить игрока по срабатываниям метрик: исключения и админы
    /// в режиме LogOnly защищены, но состояние РПУ (раунд, эвакуация, cvar) не влияет.</summary>
    public bool PerformanceDisconnectAllowed(ICommonSession session)
    {
        if (IsExempt(session))
            return false;

        return !IsActiveAdmin(session) || _adminMode != RpuAdminMode.LogOnly;
    }

    /// <summary>Действуют ли на игрока «тихие» эффекты и автобан.</summary>
    public bool PunishmentAllowed(ICommonSession session)
    {
        if (!Active || IsExempt(session))
            return false;

        // Режим только учёта снимает эффекты с действующих админов; учёт кармы идёт и дальше.
        return !IsActiveAdmin(session) || _adminMode != RpuAdminMode.LogOnly;
    }

    /// <summary>
    /// Единственная точка начисления штрафа. Считается в очках:
    /// база нарушения (rpuViolation.base) + тяжесть случая в очках + все поправки в очках,
    /// но не меньше <see cref="RpuMaths.MinPenalty"/> и не больше базы на
    /// <see cref="FishCVars.RpuMaxExtraUnits"/>. Если нарушение игроку не вменяется —
    /// не снимается ничего.
    /// </summary>
    public void Penalize(ICommonSession? session,
        ProtoId<RpuViolationPrototype> violation,
        EntityUid? victim = null,
        float extra = 0f,
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

        // Карму ещё не читали (или чтение упало) — не трогаем ничего.
        if (!suspect.KarmaLoaded)
            return;

        if (_adjust.IsWaived(session, suspect, proto.Category, victim))
            return;

        var units = _adjust.CollectUnits(session, suspect, proto.Category, victim);
        var penalty = RpuMaths.Penalty(proto.Base, units, extra, _minPenalty, _maxExtraUnits);

        if (!float.IsFinite(penalty) || penalty <= 0f)
        {
            _sawmill.Error($"Refusing to apply non-finite or zero penalty {penalty} for {violation}.");
            return;
        }

        var before = suspect.Karma;
        suspect.Karma = RpuMaths.ClampKarma(suspect.Karma - penalty);
        suspect.MarkViolation(_timing.CurTime);
        suspect.Dirty = true;

        var actual = before - suspect.Karma;
        if (actual > 0f)
        {
            // Потеря идёт в окно всплеска и тогда, когда рамка по часам игрока неbanящая:
            // окно ловит короткие серии у ветеранов, у которых рамка кармы далеко.
            suspect.AddLoss(_timing.CurTime, actual, _spikeWindow * 2);
            LogKarmaChange(session, -actual, proto.Category.ToString(), victim);
            CheckChatThreshold(session, suspect, before);
        }

        // Порог автобана сверяем после любого учтённого нарушения: игрок мог встретиться
        // с ним уже на нижней рамке кармы, где фактическое снятие равно нулю.
        CheckAutoBan(session, suspect);
    }

    /// <summary>
    /// Текущий порог автобана для игрока. NegativeInfinity — «не банить никогда».
    /// Тот же расчёт, что делает CheckAutoBan, но ничего не меняя.
    /// </summary>
    public float CurrentBanThreshold(ICommonSession session)
    {
        var suspect = GetSuspect(session.UserId);

        return RpuMaths.BanThreshold(
            suspect.RoleHours,
            Tuning.BanTiers,
            suspect.BanThresholdShift,
            _cfg.GetCVar(FishCVars.RpuBanVeterans));
    }

    /// <summary>Сколько очков потеряно сейчас в окне всплеска и сам порог всплеска.</summary>
    public (float Lost, float Units, int Minutes) SpikeInfo(ICommonSession session)
    {
        var suspect = GetSuspect(session.UserId);
        var lost = suspect.LossInWindow(_timing.CurTime, _spikeWindow);

        return (lost, _spikeUnits, (int) _spikeWindow.TotalMinutes);
    }

    /// <summary>Сколько очков снятия получится за нарушение прямо сейчас; 0 — не вменяется.</summary>
    public float PreviewPenalty(ICommonSession session,
        ProtoId<RpuViolationPrototype> violation,
        EntityUid? victim = null,
        float extra = 0f)
    {
        if (IsExempt(session) || !TryGetViolation(violation, out var proto))
            return 0f;

        var suspect = GetSuspect(session.UserId);
        if (!suspect.KarmaLoaded || _adjust.IsWaived(session, suspect, proto.Category, victim))
            return 0f;

        var units = _adjust.CollectUnits(session, suspect, proto.Category, victim);
        var penalty = RpuMaths.Penalty(proto.Base, units, extra, _minPenalty, _maxExtraUnits);

        return float.IsFinite(penalty) && penalty > 0f ? penalty : 0f;
    }

    /// <summary>Начисление кармы (восстановление, отмена ошибочного штрафа).</summary>
    public void AwardKarma(ICommonSession? session, float amount, RpuCategory category, string? details = null)
    {
        if (session == null || amount <= 0f || !float.IsFinite(amount))
            return;

        var suspect = GetSuspect(session.UserId);
        if (!suspect.KarmaLoaded)
            return;

        var before = suspect.Karma;
        suspect.Karma = RpuMaths.ClampKarma(suspect.Karma + amount);

        var actual = suspect.Karma - before;
        if (actual <= 0f)
            return;

        suspect.Dirty = true;
        suspect.OpenChatLogGateIfCleared();
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
        if (!float.IsFinite(delta))
            return false;

        var suspect = GetSuspect(session.UserId);
        if (!suspect.KarmaLoaded)
            return false;

        if (Math.Abs(delta) < 0.001f)
            return true;

        var before = suspect.Karma;
        suspect.Karma = RpuMaths.ClampKarma(suspect.Karma + delta);

        var actual = suspect.Karma - before;
        if (Math.Abs(actual) < 0.001f)
            return false;

        suspect.Dirty = true;
        suspect.OpenChatLogGateIfCleared();
        LogKarmaChange(session, actual, "admin adjustment");
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // Ожидающие записи и повторы чтения идут и при выключенном учёте: иначе
        // отключение системы оставило бы подтверждение записей зависимым от включения.
        FlushPending(frameTime);

        if (!_enabled)
            return;

        _arenaAccumulator += frameTime;
        if (_arenaAccumulator >= ArenaCheckSeconds)
        {
            _arenaAccumulator = 0f;
            ArenaRunning = IsArenaMatchRunning();
        }

        _recoverAccumulator += frameTime;
        if (_recoverAccumulator >= RecoveryCheckSeconds)
        {
            var hours = _recoverAccumulator / 3600f;
            _recoverAccumulator = 0f;

            if (Active)
                RecoverHourly(hours, now);
        }

        _saveAccumulator += frameTime;
    }

    /// <summary>
    /// Периодическое обслуживание профилей: повторное чтение после сбоя БД и запись
    /// изменённой кармы. Не зависит от того, включён ли учёт.
    /// </summary>
    private void FlushPending(float frameTime)
    {
        // Накопитель идёт от реально прошедшего времени. TickRate — это число тиков
        // в секунду, а не длительность тика: при 60 тик/с он сразу перебивал бы
        // SaveCheckSeconds, и обход делался бы каждый тик.
        _loadAccumulator += frameTime;

        if (_loadAccumulator < SaveCheckSeconds)
            return;

        _loadAccumulator = 0f;
        var now = _timing.CurTime;

        foreach (var (userId, suspect) in _suspects.ToArray())
        {
            if (!suspect.KarmaLoaded &&
                !suspect.LoadInFlight &&
                now >= suspect.LoadRetryAt &&
                _players.SessionsDict.TryGetValue(userId, out var online))
            {
                _ = LoadProfile(online, CancellationToken.None);
            }

            if (!suspect.Dirty || suspect.SaveInFlight || now < suspect.SaveRetryAt)
                continue;

            SaveKarma(suspect, userId);
        }
    }

    /// <summary>
    /// Восстановление: 0.25 кармы в час раунда, если за этот час система не сработала.
    /// Выше накопленной «базы» по наигранному времени не поднимаемся.
    /// </summary>
    private void RecoverHourly(float hours, TimeSpan now)
    {
        var rate = _cfg.GetCVar(FishCVars.RpuRecoveryPerHour) * hours;
        if (rate <= 0f)
            return;

        foreach (var (userId, suspect) in _suspects.ToArray())
        {
            if (!suspect.KarmaLoaded)
                continue;

            // Целый час без срабатываний, а не «минутная проверка после минутной проверки».
            if (now - suspect.LastViolation < RecoveryQuietPeriod)
                continue;

            if (!_players.SessionsDict.TryGetValue(userId, out var session) || session.Status != SessionStatus.InGame)
                continue;

            // Часы считаются по трекерам, а не по моменту входа: ступень порога не должна
            // отставать от раунда.
            RefreshPlaytime(session, suspect);

            if (IsExempt(session) || suspect.Karma >= suspect.BaseKarma)
                continue;

            var ceiling = Math.Min(RpuMaths.KarmaMax, suspect.BaseKarma);
            var before = suspect.Karma;
            suspect.Karma = Math.Min(ceiling, suspect.Karma + rate);

            var gained = suspect.Karma - before;
            if (gained <= 0f)
                continue;

            // Любое восстановление едет в базу: профиль мог измениться на 0.3 очка,
            // а перезапуск сервера не должен его съедать.
            suspect.Dirty = true;
            suspect.OpenChatLogGateIfCleared();
            suspect.RecoveryPending += gained;

            // Дробные шаги в лог не просим: пишем, когда накопилось хотя бы 1.
            if (suspect.RecoveryPending < 1f)
                continue;

            var amount = suspect.RecoveryPending;
            suspect.RecoveryPending = 0f;
            LogKarmaChange(session, amount, "passive recovery");
        }
    }

    /// <summary>
    /// Ставит карму в базу. Одновременно для одного игрока летит не больше одной записи:
    /// иначе более старое значение может отработаться позже и затереть более свежее.
    /// Подтверждаем запись только успешным ответом базы, при ошибке повторяем позже.
    /// </summary>
    private void SaveKarma(RpuSuspect suspect, NetUserId userId)
    {
        if (!suspect.KarmaLoaded)
            return;

        if (suspect.SaveInFlight)
        {
            suspect.Dirty = true;
            return;
        }

        var value = suspect.Karma;
        suspect.SaveInFlight = true;
        suspect.Dirty = false;

        SaveKarmaCore(suspect, userId, value);
    }

    private async void SaveKarmaCore(RpuSuspect suspect, NetUserId userId, float value)
    {
        try
        {
            await _db.SetFishRpuKarmaAsync(userId.UserId, value);
            FinishSave(suspect, userId, value, null);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to save karma for {userId}: {e}");
            FinishSave(suspect, userId, value, e);
        }
    }

    private void FinishSave(RpuSuspect suspect, NetUserId userId, float value, Exception? error)
    {
        if (!_suspects.TryGetValue(userId, out var current) || !ReferenceEquals(current, suspect))
            return;

        suspect.SaveInFlight = false;

        if (error != null)
        {
            suspect.Dirty = true;
            suspect.SaveRetryAt = _timing.CurTime + SaveRetryDelay;
            return;
        }

        if (suspect.Karma != value)
        {
            // За время записи карма изменилась — нужна ещё одна запись.
            suspect.Dirty = true;
            return;
        }

        if (suspect.Offline)
            _suspects.Remove(userId);
    }

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
