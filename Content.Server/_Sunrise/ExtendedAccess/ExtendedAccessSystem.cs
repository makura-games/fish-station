using System.Threading;
using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Station.Components;
using Robust.Shared.Prototypes;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Sunrise.ExtendedAccess;

public sealed class ExtendedAccessSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;

    private readonly Dictionary<EntityUid, StationAccessState> _stationStates = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
        SubscribeLocalEvent<AdditionalAlertLevelChangedEvent>(OnAdditionalAlertLevelChanged);
        SubscribeLocalEvent<AlertLevelComponent, EntityTerminatingEvent>(OnStationTerminating);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ClearStationStates());
    }


    /// <summary>
    /// Schedules an update of temporary access groups after an alert level change.
    /// </summary>
    private void OnAlertLevelChanged(AlertLevelChangedEvent ev)
    {
        // Это случай первичного установления кода(зеленый) по умолчанию
        // Чтобы в начале раунда не слышать, что доступы изменились на зеленый
        if (ev.PreviousLevel == string.Empty)
        {
            GetStationState(ev.Station, ev.AlertLevel).PrimaryLevel = ev.AlertLevel;
            return;
        }

        if (!TryComp<AlertLevelComponent>(ev.Station, out var alert))
            return;

        if (alert.AlertLevels == null)
            return;

        var state = GetStationState(ev.Station, ev.PreviousLevel);
        CancelUpdate(state, ev.PreviousLevel);

        if (!alert.AlertLevels.Levels.TryGetValue(alert.CurrentLevel, out var currentLevelDetail)
            || currentLevelDetail.ExtendedAccessOptions is not { } options)
        {
            state.PrimaryLevel = alert.CurrentLevel;
            ApplyAccessUpdate((ev.Station, alert), state, announceAccessGrant: false);
            return;
        }

        ScheduleAccessUpdate((ev.Station, alert), state, alert.CurrentLevel, options, isAdditional: false);
    }

    private void OnAdditionalAlertLevelChanged(AdditionalAlertLevelChangedEvent ev)
    {
        if (!TryComp<AlertLevelComponent>(ev.Station, out var alert)
            || alert.AlertLevels == null)
        {
            return;
        }

        var state = GetStationState(ev.Station, alert.CurrentLevel);

        // Отозванные временные доступы должны исчезать сразу, а не после задержки их выдачи.
        if (!ev.Enabled)
        {
            CancelUpdate(state, ev.AlertLevel);
            state.AdditionalLevels.Remove(ev.AlertLevel);
            ApplyAccessUpdate((ev.Station, alert), state, announceAccessGrant: false);
            return;
        }

        if (alert.AlertLevels.Levels.TryGetValue(ev.AlertLevel, out var detail)
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
        string level,
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

        if (announceAccessGrant && options.Announcement != null)
        {
            // В строке локализации оповещения обязательно должно быть указан параметр для времени
            var message = Loc.GetString(options.Announcement, ("time", options.Delay.TotalSeconds));

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
        string level,
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
            if (station.Comp.CurrentLevel != level)
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

        var activeLevels = new List<string> { state.PrimaryLevel };
        foreach (var level in state.AdditionalLevels)
        {
            if (station.Comp.ActiveAdditionalLevels.Contains(level))
                activeLevels.Add(level);
        }

        var globalGroups = new HashSet<ProtoId<AccessGroupPrototype>>();
        foreach (var level in activeLevels)
        {
            if (station.Comp.AlertLevels!.Levels.TryGetValue(level, out var detail)
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
                state.PrimaryLevel,
                activeLevels,
                globalGroups);
        }
    }

    private StationAccessState GetStationState(EntityUid station, string primaryLevel)
    {
        if (_stationStates.TryGetValue(station, out var state))
            return state;

        state = new StationAccessState(primaryLevel);
        _stationStates.Add(station, state);
        return state;
    }

    private static void CancelUpdate(StationAccessState state, string level)
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

    private sealed class StationAccessState(string primaryLevel)
    {
        public string PrimaryLevel = primaryLevel;
        public readonly HashSet<string> AdditionalLevels = [];
        public readonly Dictionary<string, CancellationTokenSource> Tokens = [];
    }
}
