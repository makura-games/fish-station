using System.Threading;
using Content.Server.Chat.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.AlertLevel;
using Content.Shared.GameTicking;
using Content.Shared.Station.Components;
using Robust.Shared.Prototypes;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Sunrise.ExtendedAccess;

public sealed partial class ExtendedAccessSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private AlertLevelSystem _alertLevel = default!;

    private readonly Dictionary<EntityUid, StationAccessState> _stationStates = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SunriseAlertLevelChangedEvent>(OnAlertLevelChanged);
        SubscribeLocalEvent<AdditionalAlertLevelChangedEvent>(OnAdditionalAlertLevelChanged);
        SubscribeLocalEvent<AlertLevelComponent, EntityTerminatingEvent>(OnStationTerminating);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ClearStationStates());
    }


    /// <summary>
    /// Schedules an update of temporary access groups after an alert level change.
    /// </summary>
    private void OnAlertLevelChanged(ref SunriseAlertLevelChangedEvent ev)
    {
        // Это случай первичного установления кода(зеленый) по умолчанию
        // Чтобы в начале раунда не слышать, что доступы изменились на зеленый
        if (ev.PreviousLevel == default)
        {
            GetStationState(ev.Station, ev.AlertLevel).PrimaryLevel = ev.AlertLevel;
            return;
        }

        if (!TryComp<AlertLevelComponent>(ev.Station, out var alert))
            return;

        if (!_alertLevel.TryGetLevel((ev.Station, alert), out var currentLevelId)
            || currentLevelId is not { } currentLevel)
        {
            return;
        }

        var state = GetStationState(ev.Station, ev.PreviousLevel);
        CancelUpdate(state, ev.PreviousLevel);

        if (!ProtoMan.Resolve(currentLevel, out var currentLevelDetail)
            || currentLevelDetail.ExtendedAccessOptions is not { } options)
        {
            state.PrimaryLevel = currentLevel;
            ApplyAccessUpdate((ev.Station, alert), state, announceAccessGrant: false);
            return;
        }

        ScheduleAccessUpdate((ev.Station, alert), state, currentLevel, options, isAdditional: false);
    }

    private void OnAdditionalAlertLevelChanged(ref AdditionalAlertLevelChangedEvent ev)
    {
        if (!TryComp<AlertLevelComponent>(ev.Station, out var alert))
            return;

        var primaryLevel = alert.CurrentAlertLevel;
        var state = GetStationState(ev.Station, primaryLevel);

        // Отозванные временные доступы должны исчезать сразу, а не после задержки их выдачи.
        if (!ev.Enabled)
        {
            CancelUpdate(state, ev.AlertLevel);
            state.AdditionalLevels.Remove(ev.AlertLevel);
            ApplyAccessUpdate((ev.Station, alert), state, announceAccessGrant: false);
            return;
        }

        if (ProtoMan.Resolve(ev.AlertLevel, out var detail)
            && detail.ExtendedAccessOptions is { } options)
        {
            ScheduleAccessUpdate((ev.Station, alert), state, ev.AlertLevel, options, isAdditional: true);
        }
    }

    private void OnStationTerminating(Entity<AlertLevelComponent> station, ref EntityTerminatingEvent _)
    {
        RemoveStationState(station);
    }

    private void ScheduleAccessUpdate(
        Entity<AlertLevelComponent> station,
        StationAccessState state,
        ProtoId<AlertLevelPrototype> level,
        ExtendedAccessOptions options,
        bool isAdditional,
        bool announceAccessGrant = true)
    {
        // У каждого уровня собственная задержка, чтобы быстрый код не активировал ожидающие доступы другого кода.
        CancelUpdate(state, level);
        var token = new CancellationTokenSource();
        state.Tokens[level] = token;

        Timer.Spawn(
            options.Delay,
            () => AfterDelay(station, level, token, isAdditional, announceAccessGrant),
            token.Token);

        if (announceAccessGrant && options.Announcement is { } announcement)
        {
            // В строке локализации оповещения обязательно должно быть указан параметр для времени
            var message = Loc.GetString(announcement, ("time", options.Delay.TotalSeconds));

            _chat.DispatchStationAnnouncement(station,
                message,
                colorOverride: Color.Yellow,
                sender: Loc.GetString("access-system-sender"));
        }
    }

    /// <summary>
    /// Marks the delayed level as granted and refreshes temporary access groups.
    /// </summary>
    private void AfterDelay(
        Entity<AlertLevelComponent> station,
        ProtoId<AlertLevelPrototype> level,
        CancellationTokenSource token,
        bool isAdditional,
        bool announceAccessGrant)
    {
        if (!_stationStates.TryGetValue(station, out var state)
            || !state.Tokens.TryGetValue(level, out var currentToken)
            || currentToken != token)
        {
            return;
        }

        state.Tokens.Remove(level);
        token.Dispose();

        if (TerminatingOrDeleted(station))
        {
            RemoveStationState(station);
            return;
        }

        if (isAdditional)
        {
            if (!station.Comp.ActiveAdditionalLevels.Contains(level))
                return;

            state.AdditionalLevels.Add(level);
        }
        else
        {
            if (station.Comp.CurrentAlertLevel != level)
                return;

            state.PrimaryLevel = level;
        }

        ApplyAccessUpdate(station, state, announceAccessGrant);
    }

    /// <summary>
    /// Applies the combined granted access groups from the primary and additional alert levels.
    /// </summary>
    private void ApplyAccessUpdate(
        Entity<AlertLevelComponent> station,
        StationAccessState state,
        bool announceAccessGrant)
    {
        if (announceAccessGrant)
        {
            _chat.DispatchStationAnnouncement(station,
                Loc.GetString("access-system-accesses-established"),
                colorOverride: Color.Yellow,
                sender: Loc.GetString("access-system-sender"));
        }

        var activeLevels = new List<string> { state.PrimaryLevel.Id };
        foreach (var level in state.AdditionalLevels)
        {
            if (station.Comp.ActiveAdditionalLevels.Contains(level))
                activeLevels.Add(level.Id);
        }

        var globalGroups = new HashSet<ProtoId<AccessGroupPrototype>>();
        foreach (var level in activeLevels)
        {
            if (ProtoMan.Resolve(new ProtoId<AlertLevelPrototype>(level), out var detail)
                && detail.ExtendedAccessOptions?.AccessGroup is { } group)
            {
                globalGroups.Add(group);
            }
        }

        var query = EntityQueryEnumerator<AccessReaderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var reader, out var xform))
        {
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != station)
                continue;

            if (reader.AlertAccesses.Count == 0)
                continue;

            _accessReader.UpdateAccess(
                (uid, reader),
                state.PrimaryLevel.Id,
                activeLevels,
                globalGroups);
        }
    }

    private StationAccessState GetStationState(EntityUid station, ProtoId<AlertLevelPrototype> primaryLevel)
    {
        if (_stationStates.TryGetValue(station, out var state))
            return state;

        state = new StationAccessState(primaryLevel);
        _stationStates.Add(station, state);
        return state;
    }

    private static void CancelUpdate(StationAccessState state, ProtoId<AlertLevelPrototype> level)
    {
        if (!state.Tokens.Remove(level, out var token))
            return;

        token.Cancel();
        token.Dispose();
    }

    private void RemoveStationState(EntityUid station)
    {
        if (!_stationStates.Remove(station, out var state))
            return;

        CancelUpdates(state);
    }

    private void ClearStationStates()
    {
        foreach (var state in _stationStates.Values)
        {
            CancelUpdates(state);
        }

        _stationStates.Clear();
    }

    private static void CancelUpdates(StationAccessState state)
    {
        foreach (var token in state.Tokens.Values)
        {
            token.Cancel();
            token.Dispose();
        }

        state.Tokens.Clear();
    }

    private sealed class StationAccessState(ProtoId<AlertLevelPrototype> primaryLevel)
    {
        public ProtoId<AlertLevelPrototype> PrimaryLevel = primaryLevel;
        public readonly HashSet<ProtoId<AlertLevelPrototype>> AdditionalLevels = [];
        public readonly Dictionary<ProtoId<AlertLevelPrototype>, CancellationTokenSource> Tokens = [];
    }
}
