using System.Diagnostics.CodeAnalysis;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Research.Systems;
using Content.Shared._Fish.Research.Bluespace;
using Content.Shared.ActionBlocker;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Research.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server._Fish.Research.Bluespace;

/// <summary>Runs powered station research probers and suppressors without offline point accrual.</summary>
public sealed partial class FishBluespaceResearchSystem : EntitySystem
{
    [Dependency] private FishBluespaceBackgroundSystem _background = default!;
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private PowerStateSystem _powerState = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private EntityQuery<TransformComponent> _transformQuery = default!;
    [Dependency] private EntityQuery<MetaDataComponent> _metaQuery = default!;
    [Dependency] private EntityQuery<PowerStateComponent> _powerStateQuery = default!;
    [Dependency] private EntityQuery<ApcPowerReceiverComponent> _receiverQuery = default!;
    [Dependency] private EntityQuery<FishBluespaceBackgroundComponent> _backgroundQuery = default!;

    private const float PublishInterval = 1f;
    private const float PayoutInterval = 1f;
    private const float MaximumPointsPerSecond = 1_000_000f;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<FishBluespaceProberComponent>(FishBluespaceDeviceUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnProberUiOpened);
            subs.Event<FishBluespaceSetEnabledMessage>(OnProberSetEnabled);
        });
        Subs.BuiEvents<FishBluespaceSuppressorComponent>(FishBluespaceDeviceUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnSuppressorUiOpened);
            subs.Event<FishBluespaceSetEnabledMessage>(OnSuppressorSetEnabled);
        });
        SubscribeLocalEvent<FishBluespaceProberComponent, PowerChangedEvent>(OnProberPowerChanged);
        SubscribeLocalEvent<FishBluespaceProberComponent, AnchorStateChangedEvent>(OnProberAnchorChanged);
        SubscribeLocalEvent<FishBluespaceProberComponent, ResearchRegistrationChangedEvent>(OnProberServerChanged);
        SubscribeLocalEvent<FishBluespaceProberComponent, EntityPausedEvent>(OnProberPaused);
        SubscribeLocalEvent<FishBluespaceProberComponent, ComponentShutdown>(OnProberShutdown);
        SubscribeLocalEvent<FishBluespaceSuppressorComponent, AnchorStateChangedEvent>(OnSuppressorAnchorChanged);
        SubscribeLocalEvent<FishBluespaceSuppressorComponent, EntityPausedEvent>(OnSuppressorPaused);
        SubscribeLocalEvent<FishBluespaceSuppressorComponent, ComponentShutdown>(OnSuppressorShutdown);
    }

    private void OnProberUiOpened(Entity<FishBluespaceProberComponent> ent, ref BoundUIOpenedEvent args)
    {
        PublishProber(ent);
    }

    private void OnSuppressorUiOpened(Entity<FishBluespaceSuppressorComponent> ent, ref BoundUIOpenedEvent args)
    {
        PublishSuppressor(ent);
    }

    private void OnProberSetEnabled(Entity<FishBluespaceProberComponent> ent, ref FishBluespaceSetEnabledMessage args)
    {
        TrySetEnabled(ent, args.Actor, args.Enabled);
    }

    private void OnSuppressorSetEnabled(Entity<FishBluespaceSuppressorComponent> ent, ref FishBluespaceSetEnabledMessage args)
    {
        TrySetEnabled(ent, args.Actor, args.Enabled);
    }

    private void OnProberPowerChanged(Entity<FishBluespaceProberComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            ResetAccrual(ent.Comp);
    }

    private void OnProberAnchorChanged(Entity<FishBluespaceProberComponent> ent, ref AnchorStateChangedEvent args)
    {
        ResetAccrual(ent.Comp);
        if (!args.Anchored)
            StopDemand(ent);
    }

    private void OnProberServerChanged(Entity<FishBluespaceProberComponent> ent, ref ResearchRegistrationChangedEvent args)
    {
        ResetAccrual(ent.Comp);
        StopDemand(ent);
    }

    private void OnProberPaused(Entity<FishBluespaceProberComponent> ent, ref EntityPausedEvent args)
    {
        ResetAccrual(ent.Comp);
        StopDemand(ent);
    }

    private void OnProberShutdown(Entity<FishBluespaceProberComponent> ent, ref ComponentShutdown args)
    {
        ResetAccrual(ent.Comp);
        StopDemand(ent);
    }

    private void OnSuppressorAnchorChanged(Entity<FishBluespaceSuppressorComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            StopDemand(ent);
    }

    private void OnSuppressorPaused(Entity<FishBluespaceSuppressorComponent> ent, ref EntityPausedEvent args)
    {
        StopDemand(ent);
    }

    private void OnSuppressorShutdown(Entity<FishBluespaceSuppressorComponent> ent, ref ComponentShutdown args)
    {
        StopDemand(ent);
    }

    /// <summary>Applies an explicit prober operating request from a currently authorized UI actor.</summary>
    public bool TrySetEnabled(Entity<FishBluespaceProberComponent> ent, EntityUid user, bool enabled)
    {
        if (!CanSetEnabled(ent, user))
            return false;

        ent.Comp.Enabled = enabled;
        ResetAccrual(ent.Comp);
        UpdateProberDemand(ent, out _);
        return true;
    }

    /// <summary>Checks actor, open interface, action blockers and unobstructed control range.</summary>
    public bool CanSetEnabled(Entity<FishBluespaceProberComponent> ent, EntityUid user)
    {
        return CanControl(ent, user);
    }

    /// <summary>Applies an explicit suppressor request; the station scram never prevents suppression.</summary>
    public bool TrySetEnabled(Entity<FishBluespaceSuppressorComponent> ent, EntityUid user, bool enabled)
    {
        if (!CanSetEnabled(ent, user))
            return false;

        ent.Comp.Enabled = enabled;
        UpdateSuppressorDemand(ent);
        return true;
    }

    /// <summary>Checks actor, open interface, action blockers and unobstructed control range.</summary>
    public bool CanSetEnabled(Entity<FishBluespaceSuppressorComponent> ent, EntityUid user)
    {
        return CanControl(ent, user);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!float.IsFinite(frameTime) || frameTime <= 0f)
            return;

        var elapsed = Math.Min(frameTime, 1f);
        var backgrounds = EntityQueryEnumerator<FishBluespaceBackgroundComponent>();
        while (backgrounds.MoveNext(out _, out var background))
            background.ActiveProbers = 0;

        // Fish-edit: считаем только реально работающие проберы, не сохраняя межстанционный кэш.
        var counts = EntityQueryEnumerator<FishBluespaceProberComponent>();
        while (counts.MoveNext(out var uid, out var prober))
        {
            if (!UpdateProberDemand((uid, prober), out var scope) || !IsReceivingWorkingPower(uid))
                continue;

            EnsureComp<FishBluespaceBackgroundComponent>(scope).ActiveProbers++;
        }

        var probers = EntityQueryEnumerator<FishBluespaceProberComponent>();
        while (probers.MoveNext(out var uid, out var prober))
        {
            UpdateProber((uid, prober), elapsed);
            prober.UiElapsed = Math.Min(PublishInterval, prober.UiElapsed + elapsed);
            PublishProber((uid, prober));
        }

        var suppressors = EntityQueryEnumerator<FishBluespaceSuppressorComponent>();
        while (suppressors.MoveNext(out var uid, out var suppressor))
        {
            if (UpdateSuppressorDemand((uid, suppressor)) && IsReceivingWorkingPower(uid))
                _background.TryAdjustLevel(uid, -FiniteRate(suppressor.SuppressionPerSecond, 100f) * elapsed);

            suppressor.UiElapsed = Math.Min(PublishInterval, suppressor.UiElapsed + elapsed);
            PublishSuppressor((uid, suppressor));
        }
    }

    private bool CanControl(EntityUid uid, EntityUid user)
    {
        return !TerminatingOrDeleted(uid) && !TerminatingOrDeleted(user) &&
               _metaQuery.TryComp(uid, out var meta) && !meta.EntityPaused &&
               _metaQuery.TryComp(user, out var userMeta) && !userMeta.EntityPaused &&
               _ui.IsUiOpen(uid, FishBluespaceDeviceUiKey.Key, user) &&
               _actionBlocker.CanComplexInteract(user) && _actionBlocker.CanInteract(user, uid) &&
               _interaction.InRangeUnobstructed(user, uid);
    }

    private bool CanOperateAtStation(EntityUid uid, out EntityUid scope)
    {
        scope = default;
        return !TerminatingOrDeleted(uid) && _metaQuery.TryComp(uid, out var meta) && !meta.EntityPaused &&
               _transformQuery.TryComp(uid, out var transform) && transform.Anchored &&
               transform.GridUid is not null && transform.MapUid is not null &&
               !_container.IsEntityOrParentInContainer(uid, xform: transform) &&
               _background.TryGetScope(uid, out scope) &&
               _metaQuery.TryComp(scope, out var stationMeta) && !stationMeta.EntityPaused;
    }

    private bool TryGetResearchServer(EntityUid uid, EntityUid scope,
        [NotNullWhen(true)] out EntityUid? server, [NotNullWhen(true)] out ResearchServerComponent? serverComp)
    {
        server = null;
        serverComp = null;
        if (!_research.TryGetClientServer(uid, out var linkedServer, out var linkedComp) ||
            TerminatingOrDeleted(linkedServer.Value) ||
            !_metaQuery.TryComp(linkedServer.Value, out var serverMeta) || serverMeta.EntityPaused ||
            !_background.TryGetScope(linkedServer.Value, out var serverScope) || serverScope != scope ||
            !this.IsPowered(linkedServer.Value, EntityManager))
            return false;

        server = linkedServer;
        serverComp = linkedComp;
        return true;
    }

    private bool UpdateProberDemand(Entity<FishBluespaceProberComponent> ent, out EntityUid scope)
    {
        scope = default;
        var demand = ent.Comp.Enabled && CanOperateAtStation(ent, out scope) &&
                     !_background.IsCritical(ent) && TryGetResearchServer(ent, scope, out _, out _);
        return SetDemand(ent, demand);
    }

    private bool UpdateSuppressorDemand(Entity<FishBluespaceSuppressorComponent> ent)
    {
        var demand = ent.Comp.Enabled && CanOperateAtStation(ent, out _);
        return SetDemand(ent, demand);
    }

    private bool SetDemand(EntityUid uid, bool demand)
    {
        if (TerminatingOrDeleted(uid) || !_powerStateQuery.TryComp(uid, out var powerState))
            return false;

        demand &= float.IsFinite(powerState.WorkingPowerDraw) && powerState.WorkingPowerDraw > 0f;
        _powerState.SetWorkingState((uid, powerState), demand);
        return demand;
    }

    private void StopDemand(EntityUid uid)
    {
        SetDemand(uid, false);
    }

    private bool IsReceivingWorkingPower(EntityUid uid)
    {
        return _powerStateQuery.TryComp(uid, out var powerState) && powerState.IsWorking &&
               float.IsFinite(powerState.WorkingPowerDraw) && powerState.WorkingPowerDraw > 0f &&
               _receiverQuery.TryComp(uid, out var receiver) && receiver.Powered && !receiver.PowerDisabled &&
               float.IsFinite(receiver.PowerReceived) && receiver.PowerReceived > 0f &&
               receiver.PowerReceived >= powerState.WorkingPowerDraw * 0.99f;
    }

    private void UpdateProber(Entity<FishBluespaceProberComponent> ent, float elapsed)
    {
        if (!UpdateProberDemand(ent, out var scope) || !IsReceivingWorkingPower(ent) ||
            !TryGetResearchServer(ent, scope, out var server, out var serverComp) ||
            !_backgroundQuery.TryComp(scope, out var background))
        {
            ResetAccrual(ent.Comp);
            return;
        }

        if (ent.Comp.LastServer != server || ent.Comp.LastScope != scope)
            ResetAccrual(ent.Comp);

        ent.Comp.LastServer = server;
        ent.Comp.LastScope = scope;
        var count = Math.Max(1, background.ActiveProbers);
        var exponent = float.IsFinite(ent.Comp.DiminishingExponent)
            ? Math.Clamp(ent.Comp.DiminishingExponent, 0.1f, 1f)
            : 0.5f;
        var boost = 1f + FiniteRate(ent.Comp.BackgroundPointMultiplier, 10f) * _background.GetLevel(ent) / 100f;
        var rate = Math.Min(MaximumPointsPerSecond,
            FiniteRate(ent.Comp.PointsPerSecond, MaximumPointsPerSecond) * boost / MathF.Pow(count, exponent));

        if (!_background.TryAdjustLevel(ent, FiniteRate(ent.Comp.BackgroundPerSecond, 100f) * elapsed) ||
            _background.IsCritical(ent))
        {
            ResetAccrual(ent.Comp);
            StopDemand(ent);
            return;
        }

        ent.Comp.PointAccumulator += rate * elapsed;
        ent.Comp.AccrualElapsed += elapsed;
        if (ent.Comp.AccrualElapsed < PayoutInterval)
            return;

        var seconds = ent.Comp.AccrualElapsed;
        ent.Comp.AccrualElapsed = 0f;
        var points = (int) Math.Floor(ent.Comp.PointAccumulator);
        ent.Comp.PointAccumulator -= points;
        var before = serverComp.Points;
        // Fish-edit: передаём целые очки нативному population scaling, дробь живёт только до прерывания работы.
        _research.ModifyServerResearchPoints(server.Value, points, serverComp);
        ent.Comp.PointsPerMinute = Math.Max(0f, ((long) serverComp.Points - before) * 60f / seconds);
    }

    private static void ResetAccrual(FishBluespaceProberComponent prober)
    {
        prober.PointAccumulator = 0d;
        prober.AccrualElapsed = 0f;
        prober.PointsPerMinute = 0f;
        prober.LastServer = null;
        prober.LastScope = null;
    }

    private void PublishProber(Entity<FishBluespaceProberComponent> ent)
    {
        if (ent.Comp.UiElapsed < PublishInterval)
            return;

        ent.Comp.UiElapsed = 0f;
        var hasStation = _background.TryGetScope(ent, out var scope);
        var hasServer = hasStation && TryGetResearchServer(ent, scope, out _, out _);
        Publish(ent, FishBluespaceDeviceKind.Prober, ent.Comp.Enabled, hasStation, hasServer,
            ent.Comp.PointsPerMinute, ent.Comp.Enabled && hasServer && IsReceivingWorkingPower(ent) &&
            CanOperateAtStation(ent, out _) && !_background.IsCritical(ent));
    }

    private void PublishSuppressor(Entity<FishBluespaceSuppressorComponent> ent)
    {
        if (ent.Comp.UiElapsed < PublishInterval)
            return;

        ent.Comp.UiElapsed = 0f;
        var hasStation = _background.TryGetScope(ent, out _);
        Publish(ent, FishBluespaceDeviceKind.Suppressor, ent.Comp.Enabled, hasStation, true, 0f,
            ent.Comp.Enabled && IsReceivingWorkingPower(ent) && CanOperateAtStation(ent, out _));
    }

    private void Publish(EntityUid uid, FishBluespaceDeviceKind kind, bool enabled, bool hasStation,
        bool hasServer, float pointsPerMinute, bool working)
    {
        var level = _background.GetLevel(uid);
        var critical = _background.IsCritical(uid);
        var unstable = critical;
        if (hasStation && _background.TryGetScope(uid, out var scope) &&
            _backgroundQuery.TryComp(scope, out var background))
        {
            var danger = float.IsFinite(background.DangerThreshold)
                ? Math.Clamp(background.DangerThreshold, 0f, 100f)
                : 85f;
            unstable |= level >= danger;
        }
        _appearance.SetData(uid, FishBluespaceDeviceVisuals.Working, working);
        _appearance.SetData(uid, FishBluespaceDeviceVisuals.Unstable, unstable);
        if (!_ui.IsUiOpen(uid, FishBluespaceDeviceUiKey.Key))
            return;

        var received = _receiverQuery.TryComp(uid, out var receiver) ? FiniteRate(receiver.PowerReceived, float.MaxValue) : 0f;
        var requested = receiver == null ? 0f : FiniteRate(receiver.Load, float.MaxValue);
        _ui.SetUiState(uid, FishBluespaceDeviceUiKey.Key, new FishBluespaceDeviceBoundUserInterfaceState(
            kind, enabled, receiver?.Powered ?? false,
            _transformQuery.TryComp(uid, out var transform) && transform.Anchored,
            hasStation, hasServer, critical, level, received, requested, 0f, pointsPerMinute,
            FishBluespacePowerMode.Normal, FishBluespaceLootCategory.Research));
    }

    private static float FiniteRate(float rate, float maximum)
    {
        return float.IsFinite(rate) ? Math.Clamp(rate, 0f, maximum) : 0f;
    }
}
