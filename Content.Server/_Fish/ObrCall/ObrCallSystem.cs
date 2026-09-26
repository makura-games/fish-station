using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Server._Sunrise.StationCentComm;
using Content.Server._Sunrise.Storyteller.Components;
using Content.Shared._Fish.ObrCall;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.ObrCall;

/// <summary>
/// Общая система запроса ОБР: валидация, покупка, GameRule, FTL, миссия.
/// Консоли ЦК и станции — только источники запроса.
/// </summary>
public sealed partial class ObrCallSystem : EntitySystem
{
    public const string ObrMissionMindRoleId = "MindRoleObrMission";

    private static readonly TimeSpan CallLockDuration = TimeSpan.FromSeconds(3);

    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private CargoSystem _cargo = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private RoleSystem _roles = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private ShuttleSystem _shuttles = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    /// <summary>
    /// Активные вызовы: teamId → rule entity.
    /// </summary>
    private readonly Dictionary<string, EntityUid> _activeCalls = new();

    /// <summary>
    /// Очередь отложенных вызовов со станционной консоли (15-минутная задержка).
    /// </summary>
    private readonly List<PendingStationObrCall> _pendingCalls = new();

    /// <summary>
    /// Очередь отложенных возвратов средств при сбое вызова.
    /// </summary>
    private readonly List<PendingObrRefund> _pendingRefunds = new();

    /// <summary>
    /// Блокировка от двойных кликов.
    /// </summary>
    private TimeSpan _callLockUntil;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("obr.call");

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        SubscribeLocalEvent<ObrCentCommConsoleComponent, BoundUIOpenedEvent>(OnCentCommUiOpened);
        SubscribeLocalEvent<ObrStationConsoleComponent, BoundUIOpenedEvent>(OnStationUiOpened);

        SubscribeLocalEvent<ObrCentCommConsoleComponent, ObrCallRequestMessage>(OnCentCommRequest);
        SubscribeLocalEvent<ObrStationConsoleComponent, ObrCallRequestMessage>(OnStationRequest);

        SubscribeLocalEvent<GameRuleEndedEvent>(OnGameRuleEnded);

        SubscribeLocalEvent<ObrMissionTargetComponent, MindAddedMessage>(OnMissionTargetMindAdded);
        SubscribeLocalEvent<ObrMissionBriefingComponent, GetBriefingEvent>(OnGetMissionBriefing);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _activeCalls.Clear();
        _pendingCalls.Clear();
        _pendingRefunds.Clear();
        _callLockUntil = TimeSpan.Zero;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingRefunds.Count > 0)
        {
            for (var i = _pendingRefunds.Count - 1; i >= 0; i--)
            {
                var refund = _pendingRefunds[i];
                if (TryProcessRefund(refund))
                    _pendingRefunds.RemoveAt(i);
            }
        }

        if (_pendingCalls.Count == 0)
            return;

        var now = _timing.CurTime;
        for (var i = _pendingCalls.Count - 1; i >= 0; i--)
        {
            var call = _pendingCalls[i];
            if (now < call.ExecuteTime)
                continue;

            _pendingCalls.RemoveAt(i);
            ExecutePendingCall(call);
        }
    }

    private void OnGameRuleEnded(ref GameRuleEndedEvent args)
    {
        var ruleEntity = args.RuleEntity;
        var toRemove = _activeCalls
            .Where(kv => kv.Value == ruleEntity)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in toRemove)
            _activeCalls.Remove(key);
    }

    private void OnCentCommUiOpened(Entity<ObrCentCommConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent.Owner, purchaseMode: false, args.Actor);
    }

    private void OnStationUiOpened(Entity<ObrStationConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent.Owner, purchaseMode: true, args.Actor);
    }

    private void OnCentCommRequest(Entity<ObrCentCommConsoleComponent> ent, ref ObrCallRequestMessage args)
    {
        if (args.Actor is not { Valid: true } actor)
            return;

        if (!_access.IsAllowed(actor, ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("obr-call-error-access"), ent, actor, PopupType.MediumCaution);
            return;
        }

        TryProcessCall(actor, ent.Owner, args.TeamId, args.Mission, purchaseMode: false);
    }

    private void OnStationRequest(Entity<ObrStationConsoleComponent> ent, ref ObrCallRequestMessage args)
    {
        if (args.Actor is not { Valid: true } actor)
            return;

        if (!_access.IsAllowed(actor, ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("obr-call-error-access"), ent, actor, PopupType.MediumCaution);
            return;
        }

        TryProcessCall(actor, ent.Owner, args.TeamId, args.Mission, purchaseMode: true);
    }

    private void TryProcessCall(
        EntityUid actor,
        EntityUid console,
        string teamId,
        string mission,
        bool purchaseMode)
    {
        var maxMissionLength = purchaseMode
            ? CompOrNull<ObrStationConsoleComponent>(console)?.MaxMissionLength ?? 512
            : CompOrNull<ObrCentCommConsoleComponent>(console)?.MaxMissionLength ?? 512;

        if (_timing.CurTime < _callLockUntil)
        {
            Fail(console, actor, Loc.GetString("obr-call-error-busy"), purchaseMode);
            return;
        }

        _callLockUntil = _timing.CurTime + CallLockDuration;

        var settings = _prototypes.Index(DefaultSettingsId);
        var roundDuration = _gameTicker.RoundDuration();
        if (roundDuration < settings.EarliestCallTime)
        {
            var remaining = settings.EarliestCallTime - roundDuration;
            var minutes = (int)settings.EarliestCallTime.TotalMinutes;
            var timeLeft = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
            Fail(console, actor, Loc.GetString("obr-call-error-early-round", ("minutes", minutes), ("timeLeft", timeLeft)), purchaseMode);
            return;
        }

        if (!_prototypes.TryIndex<ObrTeamPrototype>(teamId, out var team))
        {
            Fail(console, actor, Loc.GetString("obr-call-error-unknown-team"), purchaseMode);
            return;
        }

        if (purchaseMode)
        {
            if (!team.StationAvailable || team.StationCost is null)
            {
                Fail(console, actor, Loc.GetString("obr-call-error-team-unavailable"), purchaseMode);
                return;
            }
        }
        else if (!team.CentCommAvailable)
        {
            Fail(console, actor, Loc.GetString("obr-call-error-team-unavailable"), purchaseMode);
            return;
        }

        var sanitizedMission = SanitizeMission(mission, maxMissionLength);

        if (IsTeamAlreadyActive(team.ID))
        {
            Fail(console, actor, Loc.GetString("obr-call-error-already-active"), purchaseMode);
            return;
        }

        if (!TryGetTargetStation(console, purchaseMode, out var station))
        {
            Fail(console, actor, Loc.GetString("obr-call-error-no-station"), purchaseMode);
            return;
        }

        if (!TryComp<StationDataComponent>(station, out var stationData))
        {
            Fail(console, actor, Loc.GetString("obr-call-error-no-station"), purchaseMode);
            return;
        }

        if (!TryGetCentCommGrid(out var centcommGrid))
        {
            Fail(console, actor, Loc.GetString("obr-call-error-no-centcomm"), purchaseMode);
            return;
        }

        var cost = 0;
        if (purchaseMode)
        {
            cost = team.StationCost!.Value;
            if (!TryComp<StationBankAccountComponent>(station, out var bank))
            {
                Fail(console, actor, Loc.GetString("obr-call-error-no-bank"), purchaseMode);
                return;
            }

            var balance = _cargo.GetBalanceFromAccount((station, bank), team.Account);
            if (balance < cost)
            {
                Fail(console, actor, Loc.GetString("obr-call-error-insufficient-funds"), purchaseMode);
                return;
            }

            // Списание средств сразу при заказе отряда со станции.
            _cargo.UpdateBankAccount((station, bank), -cost, team.Account);

            var delay = CompOrNull<ObrStationConsoleComponent>(console)?.CallDelay ?? TimeSpan.FromMinutes(15);
            var executeTime = _timing.CurTime + delay;

            var pendingCall = new PendingStationObrCall
            {
                TeamId = team.ID,
                Account = team.Account,
                Mission = sanitizedMission,
                StationUid = station,
                Cost = cost,
                Actor = actor,
                Console = console,
                ExecuteTime = executeTime
            };
            _pendingCalls.Add(pendingCall);

            var teamName = Loc.GetString(team.Name);
            var missionNote = string.IsNullOrWhiteSpace(sanitizedMission) ? string.Empty : $"; mission: {sanitizedMission}";
            _adminLog.Add(
                LogType.Action,
                LogImpact.High,
                $"{ToPrettyString(actor)} scheduled OBR team {team.ID} via {ToPrettyString(console)} for {cost} (delay {delay.TotalMinutes}m){missionNote}");

            var formattedDuration = delay.TotalMinutes >= 1 && delay.Seconds == 0
                ? $"{(int)delay.TotalMinutes} минут"
                : $"{(int)delay.TotalMinutes:D2}:{delay.Seconds:D2}";

            // Анонс на станцию о вызове отряда с настроенной задержкой
            var announcement = Loc.GetString("obr-call-announcement-station", ("team", teamName), ("duration", formattedDuration));
            _chat.DispatchStationAnnouncement(
                station,
                announcement,
                sender: Loc.GetString("chat-manager-sender-announcement"),
                playDefaultSound: true,
                colorOverride: Color.Gold);

            var successStatus = Loc.GetString("obr-call-pending-success", ("team", teamName), ("duration", formattedDuration));
            _popup.PopupEntity(successStatus, console, actor, PopupType.Medium);
            UpdateUi(console, purchaseMode: true, actor, successStatus);
            return;
        }

        // CentComm: мгновенный вызов
        SpawnAndFtlObrShuttle(team, sanitizedMission, station, centcommGrid, actor, console, purchaseMode: false, cost: 0);
    }

    private void ExecutePendingCall(PendingStationObrCall call)
    {
        if (!_prototypes.TryIndex<ObrTeamPrototype>(call.TeamId, out var team))
        {
            Refund(call.StationUid, call.Account, call.Cost, call.TeamId);
            return;
        }

        if (IsTeamAlreadyActive(team.ID))
        {
            Refund(call.StationUid, call.Account, call.Cost, call.TeamId);
            _sawmill.Warning($"Pending OBR call {call.TeamId} cancelled because team is already active. Refunded {call.Cost}.");
            return;
        }

        if (!TryGetCentCommGrid(out var centcommGrid))
        {
            Refund(call.StationUid, call.Account, call.Cost, call.TeamId);
            _sawmill.Warning($"Pending OBR call {call.TeamId} failed: CentComm grid not found. Refunded {call.Cost}.");
            return;
        }

        SpawnAndFtlObrShuttle(team, call.Mission, call.StationUid, centcommGrid, call.Actor, call.Console, purchaseMode: true, cost: call.Cost);
    }

    private bool SpawnAndFtlObrShuttle(
        ObrTeamPrototype team,
        string mission,
        EntityUid station,
        EntityUid centcommGrid,
        EntityUid actor,
        EntityUid console,
        bool purchaseMode,
        int cost)
    {
        var charged = purchaseMode && cost > 0;

        if (!_gameTicker.StartGameRule(team.GameRule, out var ruleUid))
        {
            if (charged)
                Refund(station, team, cost);

            Fail(console, actor, Loc.GetString("obr-call-error-rule-failed"), purchaseMode);
            return false;
        }

        if (!TryComp<RuleGridsComponent>(ruleUid, out var ruleGrids) || ruleGrids.MapGrids.Count == 0)
        {
            _gameTicker.EndGameRule(ruleUid);
            if (charged)
                Refund(station, team, cost);

            Fail(console, actor, Loc.GetString("obr-call-error-shuttle-failed"), purchaseMode);
            return false;
        }

        var anyShuttle = false;
        foreach (var grid in ruleGrids.MapGrids)
        {
            if (!TryComp<ShuttleComponent>(grid, out var shuttle))
                continue;

            var called = EnsureComp<ObrCalledShuttleComponent>(grid);
            called.TeamId = team.ID;
            called.Mission = mission;
            called.RuleUid = ruleUid;

            MarkMissionTargetsOnGrid(grid);

            if (!TryFtlObrToCentCommDistantPoint(grid, shuttle, centcommGrid))
            {
                _sawmill.Warning($"OBR shuttle {ToPrettyString(grid)} could not FTL to CentComm safe distant point");
                continue;
            }

            anyShuttle = true;
        }

        if (!anyShuttle)
        {
            _gameTicker.EndGameRule(ruleUid);
            if (charged)
                Refund(station, team, cost);

            Fail(console, actor, Loc.GetString("obr-call-error-shuttle-failed"), purchaseMode);
            return false;
        }

        _activeCalls[team.ID] = ruleUid;

        var teamName = Loc.GetString(team.Name);
        var purchaseNote = charged ? $" for {cost}" : " (CentComm)";
        var missionNote = string.IsNullOrWhiteSpace(mission) ? string.Empty : $"; mission: {mission}";
        _adminLog.Add(
            LogType.Action,
            LogImpact.High,
            $"{ToPrettyString(actor)} spawned OBR team {team.ID} via {ToPrettyString(console)}{purchaseNote}{missionNote}");

        if (console.Valid && !Deleted(console))
        {
            _popup.PopupEntity(
                Loc.GetString("obr-call-success", ("team", teamName)),
                console,
                actor,
                PopupType.Medium);

            UpdateUi(console, purchaseMode, actor, Loc.GetString("obr-call-success", ("team", teamName)));
        }

        DeliverMissionToActiveMembers(ruleGrids.MapGrids, mission);
        return true;
    }

    /// <summary>
    /// Находит сетку сектора Центрального Командования.
    /// </summary>
    public bool TryGetCentCommGrid(out EntityUid centcommGrid)
    {
        centcommGrid = EntityUid.Invalid;
        var query = EntityQueryEnumerator<StationCentCommComponent>();
        while (query.MoveNext(out var uid, out var centcommComp))
        {
            if (centcommComp.Entity.Valid && !Deleted(centcommComp.Entity))
            {
                centcommGrid = centcommComp.Entity;
                return true;
            }

            if (centcommComp.MapId != MapId.Nullspace && _mapManager.MapExists(centcommComp.MapId))
            {
                foreach (var grid in _mapManager.GetAllGrids(centcommComp.MapId))
                {
                    if (grid.Owner.Valid && !Deleted(grid.Owner))
                    {
                        centcommGrid = grid.Owner;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Для покупки — основная станция с банковским счётом (как у cargo console / storyteller metrics).
    /// Для ЦК — основная игровая станция (MainStation), даже если у неё есть привязка CentComm-шаттла.
    /// </summary>
    private bool TryGetTargetStation(EntityUid console, bool purchaseMode, out EntityUid station)
    {
        station = EntityUid.Invalid;

        if (purchaseMode)
        {
            var owning = _station.GetOwningStation(console);
            if (owning != null && TrySelectTargetStation(owning.Value, purchaseMode, out station))
                return true;
        }

        foreach (var candidate in _station.GetStations())
        {
            if (TrySelectTargetStation(candidate, purchaseMode, out station))
                return true;
        }

        return false;
    }

    private bool TrySelectTargetStation(EntityUid candidate, bool purchaseMode, out EntityUid station)
    {
        station = EntityUid.Invalid;

        if (!HasComp<StationDataComponent>(candidate))
            return false;

        // Sunrise: баланс и метрики считаются только по MainStation.
        // Не фильтровать StationCentComm: он есть у StandardNanotrasenStation (привязка шаттла ЦК),
        // а не только у отдельной карты NanotrasenCentralCommand.
        if (!HasComp<MainStationComponent>(candidate))
            return false;

        if (purchaseMode && !TryComp<StationBankAccountComponent>(candidate, out _))
            return false;

        station = candidate;
        return true;
    }

    private void Refund(EntityUid station, ObrTeamPrototype team, int cost)
    {
        Refund(station, team.Account, cost, team.ID);
    }

    private void Fail(EntityUid console, EntityUid actor, string message, bool purchaseMode)
    {
        _popup.PopupEntity(message, console, actor, PopupType.MediumCaution);
        UpdateUi(console, purchaseMode, actor, message);
    }

    private void UpdateUi(EntityUid console, bool purchaseMode, EntityUid? actor = null, string? status = null)
    {
        TryGetTargetStation(console, purchaseMode, out var station);

        var balance = 0;
        if (station.Valid && TryComp<StationBankAccountComponent>(station, out var bank))
            balance = _cargo.GetBalanceFromAccount((station, bank), bank.PrimaryAccount);

        var settings = _prototypes.Index(DefaultSettingsId);
        var roundDuration = _gameTicker.RoundDuration();
        var isEarlyRound = roundDuration < settings.EarliestCallTime;
        string? earlyRoundReason = null;
        if (isEarlyRound)
        {
            var remaining = settings.EarliestCallTime - roundDuration;
            var minutes = (int)settings.EarliestCallTime.TotalMinutes;
            var timeLeft = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
            earlyRoundReason = Loc.GetString("obr-call-error-early-round", ("minutes", minutes), ("timeLeft", timeLeft));
        }

        var teams = new List<ObrCallTeamEntry>();
        foreach (var team in _prototypes.EnumeratePrototypes<ObrTeamPrototype>().OrderBy(t => t.SortOrder).ThenBy(t => t.ID))
        {
            if (purchaseMode)
            {
                if (!team.StationAvailable || team.StationCost is null)
                    continue;
            }
            else if (!team.CentCommAvailable)
            {
                continue;
            }

            var available = true;
            string? reason = null;

            if (isEarlyRound)
            {
                available = false;
                reason = earlyRoundReason;
            }
            else if (_pendingCalls.Any(p => p.TeamId == team.ID))
            {
                available = false;
                reason = Loc.GetString("obr-call-error-pending");
            }
            else if (IsTeamAlreadyActive(team.ID))
            {
                available = false;
                reason = Loc.GetString("obr-call-error-already-active");
            }
            else if (purchaseMode && team.StationCost is { } cost)
            {
                var teamBalance = balance;
                if (station.Valid && TryComp<StationBankAccountComponent>(station, out var bankForTeam))
                    teamBalance = _cargo.GetBalanceFromAccount((station, bankForTeam), team.Account);

                if (teamBalance < cost)
                {
                    available = false;
                    reason = Loc.GetString("obr-call-error-insufficient-funds");
                }
            }

            teams.Add(new ObrCallTeamEntry(
                team.ID,
                Loc.GetString(team.Name),
                team.Description != null ? Loc.GetString(team.Description) : null,
                purchaseMode ? team.StationCost : null,
                available,
                reason));
        }

        var state = new ObrCallBoundUserInterfaceState(purchaseMode, balance, status, teams);
        _ui.SetUiState(console, ObrCallUiKey.Key, state);
    }

    private static string SanitizeMission(string mission, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(mission))
            return string.Empty;

        var trimmed = mission.Trim();
        if (trimmed.Length > maxLength)
            trimmed = trimmed[..maxLength];

        return trimmed.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private void MarkMissionTargetsOnGrid(EntityUid grid)
    {
        var query = EntityQueryEnumerator<MindContainerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            EnsureComp<ObrMissionTargetComponent>(uid);
        }
    }

    private void DeliverMissionToActiveMembers(List<EntityUid> grids, string mission)
    {
        if (string.IsNullOrWhiteSpace(mission))
            return;

        var gridSet = grids.ToHashSet();
        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var actor, out var xform))
        {
            if (xform.GridUid is not { } grid || !gridSet.Contains(grid))
                continue;

            ApplyMissionToEntity(uid, actor.PlayerSession, mission);
        }
    }

    private void OnMissionTargetMindAdded(EntityUid uid, ObrMissionTargetComponent component, MindAddedMessage args)
    {
        if (Transform(uid).GridUid is not { } grid)
            return;

        if (!TryComp<ObrCalledShuttleComponent>(grid, out var called))
            return;

        if (string.IsNullOrWhiteSpace(called.Mission))
            return;

        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        ApplyMissionToEntity(uid, actor.PlayerSession, called.Mission);
    }

    private void ApplyMissionToEntity(EntityUid entity, ICommonSession session, string mission)
    {
        var text = Loc.GetString("obr-call-mission-briefing", ("mission", mission));
        _antag.SendBriefing(session, text, Color.Gold, null);

        if (!_mind.TryGetMind(entity, out var mindId, out var mindComp))
            return;

        // Обновляем существующую роль или добавляем новую.
        foreach (var role in mindComp.MindRoleContainer.ContainedEntities)
        {
            if (!TryComp<ObrMissionBriefingComponent>(role, out var existing))
                continue;

            existing.Mission = mission;
            Dirty(role, existing);
            return;
        }

        _roles.MindAddRole(mindId, ObrMissionMindRoleId, mindComp, silent: true);

        foreach (var role in mindComp.MindRoleContainer.ContainedEntities)
        {
            if (!TryComp<ObrMissionBriefingComponent>(role, out var briefing))
                continue;

            briefing.Mission = mission;
            Dirty(role, briefing);
            break;
        }
    }

    private void OnGetMissionBriefing(EntityUid uid, ObrMissionBriefingComponent component, ref GetBriefingEvent args)
    {
        if (string.IsNullOrWhiteSpace(component.Mission))
            return;

        args.Append(Loc.GetString("obr-call-mission-briefing", ("mission", component.Mission)));
    }

    private bool IsTeamAlreadyActive(string teamId)
    {
        if (_pendingCalls.Any(p => p.TeamId == teamId))
            return true;

        if (_activeCalls.TryGetValue(teamId, out var rule) &&
            Exists(rule) &&
            _gameTicker.IsGameRuleActive(rule))
        {
            return true;
        }

        // Дополнительная защита: шаттл ещё в мире после завершения rule.
        var query = EntityQueryEnumerator<ObrCalledShuttleComponent>();
        while (query.MoveNext(out var uid, out var called))
        {
            if (called.TeamId == teamId && !Deleted(uid))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Публичный API для тестов.
    /// </summary>
    public bool IsTeamActive(string teamId) => IsTeamAlreadyActive(teamId);

    private void Refund(EntityUid station, ProtoId<CargoAccountPrototype> account, int cost, string? teamId = null)
    {
        if (cost <= 0)
            return;

        var pending = new PendingObrRefund
        {
            Station = station,
            Account = account,
            Cost = cost,
            TeamId = teamId,
        };

        if (!TryProcessRefund(pending))
        {
            _pendingRefunds.Add(pending);
        }
    }

    private bool TryProcessRefund(PendingObrRefund refund)
    {
        var targetStation = refund.Station;

        if (!targetStation.Valid || !Exists(targetStation))
        {
            if (!TryGetTargetStation(EntityUid.Invalid, purchaseMode: true, out targetStation))
            {
                _sawmill.Warning($"Cannot process refund of {refund.Cost} to {refund.Account} for {refund.TeamId}: station entity {refund.Station} is invalid and no fallback station found.");
                return false;
            }

            refund.Station = targetStation;
        }

        if (!TryComp<StationBankAccountComponent>(targetStation, out var bank))
        {
            _sawmill.Warning($"StationBankAccountComponent missing on station {ToPrettyString(targetStation)} for refund of {refund.Cost} to {refund.Account}. Retrying later.");
            return false;
        }

        _cargo.UpdateBankAccount((targetStation, bank), refund.Cost, refund.Account);
        _sawmill.Info($"Refunded {refund.Cost} to {refund.Account} on station {ToPrettyString(targetStation)} after failed OBR call{(refund.TeamId != null ? $" {refund.TeamId}" : "")}");
        return true;
    }
}

/// <summary>
/// Информация об ожидающем возврате средств.
/// </summary>
public sealed class PendingObrRefund
{
    public EntityUid Station;
    public ProtoId<CargoAccountPrototype> Account = "Cargo";
    public int Cost;
    public string? TeamId;
}

/// <summary>
/// Информация об ожидающем прибытия отряде ОБР со станции.
/// </summary>
public sealed class PendingStationObrCall
{
    public string TeamId = string.Empty;
    public ProtoId<CargoAccountPrototype> Account = "Cargo";
    public string Mission = string.Empty;
    public EntityUid StationUid;
    public int Cost;
    public EntityUid Actor;
    public EntityUid Console;
    public TimeSpan ExecuteTime;
}
