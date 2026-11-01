using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Stack;
using Content.Shared._Fish.Research.Bluespace;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Power.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Research.Bluespace;

/// <summary>Converts real HV energy into bounded rewards while sharing station bluespace instability.</summary>
public sealed partial class FishBluespaceHarvesterSystem : EntitySystem
{
    [Dependency] private FishBluespaceBackgroundSystem _bluespaceBackground = default!;
    [Dependency] private PowerStateSystem _powerState = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityQuery<PowerConsumerComponent> _powerConsumerQuery = default!;
    [Dependency] private EntityQuery<PowerStateComponent> _powerStateQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _transformQuery = default!;
    [Dependency] private EntityQuery<MetaDataComponent> _metaQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;

    private const int MaximumBatchSize = 16;
    private const int MaximumBatchUnits = 300;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);
    private static readonly Vector2i[] OutputOffsets =
    [
        new(0, -2), new(2, 0), new(0, 2), new(-2, 0),
        new(0, -3), new(3, 0), new(0, 3), new(-3, 0),
    ];

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(PowerNetSystem));
        UpdatesAfter.Add(typeof(FishBluespaceBackgroundSystem));
        SubscribeLocalEvent<FishBluespaceHarvesterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FishBluespaceHarvesterComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FishBluespaceHarvesterComponent, EntityPausedEvent>(OnPaused);
        SubscribeLocalEvent<FishBluespaceHarvesterComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<FishBluespaceHarvesterComponent, EntGotInsertedIntoContainerMessage>(OnInserted);

        Subs.BuiEvents<FishBluespaceHarvesterComponent>(FishBluespaceDeviceUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<FishBluespaceSetEnabledMessage>(OnSetEnabled);
            subs.Event<FishBluespaceSetPowerModeMessage>(OnSetPowerMode);
            subs.Event<FishBluespaceSetLootCategoryMessage>(OnSetLootCategory);
        });
    }

    private void OnMapInit(Entity<FishBluespaceHarvesterComponent> ent, ref MapInitEvent args)
    {
        UpdateInterlock(ent);
        UpdatePowerDraw(ent);
        UpdateAppearance(ent);
    }

    private void OnShutdown(Entity<FishBluespaceHarvesterComponent> ent, ref ComponentShutdown args)
    {
        SetPowerDraw(ent, 0f);
        _appearance.SetData(ent, FishBluespaceDeviceVisuals.Working, false);
    }

    private void OnPaused(Entity<FishBluespaceHarvesterComponent> ent, ref EntityPausedEvent args)
    {
        SetPowerDraw(ent, 0f);
        _appearance.SetData(ent, FishBluespaceDeviceVisuals.Working, false);
    }

    private void OnAnchorChanged(Entity<FishBluespaceHarvesterComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            SetPowerDraw(ent, 0f);
    }

    private void OnInserted(Entity<FishBluespaceHarvesterComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        SetPowerDraw(ent, 0f);
    }

    /// <summary>Checks operating prerequisites and actual positive HV receipt without changing state.</summary>
    public bool CanOperate(Entity<FishBluespaceHarvesterComponent> ent)
    {
        return CanRequestPower(ent) && _powerConsumerQuery.TryComp(ent, out var consumer) &&
               float.IsFinite(consumer.DrawRate) && consumer.DrawRate > 0f &&
               float.IsFinite(consumer.ReceivedPower) && consumer.ReceivedPower > 0f;
    }

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!float.IsFinite(frameTime) || frameTime <= 0f)
            return;

        var query = EntityQueryEnumerator<FishBluespaceHarvesterComponent>();
        while (query.MoveNext(out var uid, out var harvester))
        {
            Entity<FishBluespaceHarvesterComponent> ent = (uid, harvester);
            UpdateInterlock(ent);
            UpdatePowerDraw(ent);

            // Fish-edit: после изменения нагрузки ждём новый расчёт сети; задержки не превращаем в накопленные циклы.
            if (!harvester.SkipPowerSample && CanOperate(ent))
                AccumulateEnergy(ent, MathF.Min(frameTime, 1f));
            harvester.SkipPowerSample = false;

            if (_timing.CurTime >= harvester.NextOutputCheck)
            {
                harvester.NextOutputCheck = _timing.CurTime + CheckInterval;
                if (CanOperate(ent))
                {
                    if (harvester.StoredEnergy >= harvester.EnergyPerReward)
                        TryProduceReward(ent);

                    TryOpenIncursion(ent);
                }
            }

            UpdateAppearance(ent);
            if (_timing.CurTime < harvester.NextUiUpdate)
                continue;

            harvester.NextUiUpdate = _timing.CurTime + CheckInterval;
            UpdateUi(ent);
        }
    }

    private bool CanRequestPower(Entity<FishBluespaceHarvesterComponent> ent)
    {
        return !TerminatingOrDeleted(ent) && ent.Comp.Enabled && !IsCritical(ent) &&
               float.IsFinite(ent.Comp.EnergyPerReward) && ent.Comp.EnergyPerReward > 0f &&
               float.IsFinite(ent.Comp.BackgroundPerMegajoule) && ent.Comp.BackgroundPerMegajoule >= 0f &&
               float.IsFinite(ent.Comp.OverdriveBackgroundMultiplier) && ent.Comp.OverdriveBackgroundMultiplier >= 1f &&
               GetRequestedPower(ent) > 0f && IsOnWorldFloor(ent) &&
               _powerStateQuery.HasComp(ent) &&
               _bluespaceBackground.TryGetScope(ent, out var station) &&
               _metaQuery.TryComp(station, out var stationMeta) && !stationMeta.EntityPaused &&
               _powerConsumerQuery.TryComp(ent, out var consumer) && consumer.Net != null;
    }

    private bool IsOnWorldFloor(EntityUid uid)
    {
        if (!_metaQuery.TryComp(uid, out var meta) || meta.EntityPaused ||
            !_transformQuery.TryComp(uid, out var xform) || !xform.Anchored ||
            xform.MapID == MapId.Nullspace || xform.GridUid is not { } grid ||
            xform.ParentUid != grid || _container.IsEntityOrParentInContainer(uid, meta, xform))
            return false;

        return _turf.GetTileRef(xform.Coordinates) is { } tile && tile.GridUid == grid && !_turf.IsSpace(tile);
    }

    private bool IsCritical(Entity<FishBluespaceHarvesterComponent> ent)
    {
        if (_bluespaceBackground.IsCritical(ent))
            return true;

        var threshold = ent.Comp.Critical ? ent.Comp.RestartLevel : ent.Comp.CriticalLevel;
        return !float.IsFinite(threshold) || _bluespaceBackground.GetLevel(ent) >= Math.Clamp(threshold, 0f, 100f);
    }

    private void UpdateInterlock(Entity<FishBluespaceHarvesterComponent> ent)
    {
        ent.Comp.Critical = IsCritical(ent);
        if (!ent.Comp.Critical)
            return;

        ent.Comp.Enabled = false;
        SetPowerDraw(ent, 0f);
    }

    private float GetRequestedPower(Entity<FishBluespaceHarvesterComponent> ent)
    {
        var requested = ent.Comp.PowerMode switch
        {
            FishBluespacePowerMode.Normal => ent.Comp.NormalPowerDraw,
            FishBluespacePowerMode.Overdrive => ent.Comp.OverdrivePowerDraw,
            _ => 0f,
        };
        return float.IsFinite(requested) && requested > 0f ? requested : 0f;
    }

    private void UpdatePowerDraw(Entity<FishBluespaceHarvesterComponent> ent)
    {
        var requested = CanRequestPower(ent) ? GetRequestedPower(ent) : 0f;
        SetPowerDraw(ent, requested);
    }

    private void SetPowerDraw(Entity<FishBluespaceHarvesterComponent> ent, float requested)
    {
        if (!_powerConsumerQuery.TryComp(ent, out var consumer))
            return;

        if (!_powerStateQuery.TryComp(ent, out var powerState))
        {
            consumer.DrawRate = 0f;
            return;
        }

        if (consumer.DrawRate == requested && powerState.IsWorking == (requested > 0f))
            return;

        // Fish-edit: штатный setter управляет IsWorking, а переменная HV-нагрузка задаётся через PowerConsumer.
        ent.Comp.SkipPowerSample = true;
        _powerState.SetWorkingState((ent.Owner, powerState), requested > 0f);
        consumer.DrawRate = requested;
    }

    private void AccumulateEnergy(Entity<FishBluespaceHarvesterComponent> ent, float frameTime)
    {
        var consumer = _powerConsumerQuery.Comp(ent);
        var energy = (double) MathF.Min(consumer.ReceivedPower, consumer.DrawRate) * frameTime;
        if (!double.IsFinite(energy) || energy <= 0d)
            return;

        var multiplier = ent.Comp.PowerMode == FishBluespacePowerMode.Overdrive
            ? ent.Comp.OverdriveBackgroundMultiplier
            : 1f;
        var background = energy / 1_000_000d * ent.Comp.BackgroundPerMegajoule * multiplier;
        if (!double.IsFinite(background) || background > float.MaxValue ||
            !_bluespaceBackground.TryAdjustLevel(ent, (float) background))
            return;

        if (!double.IsFinite(ent.Comp.StoredEnergy) || ent.Comp.StoredEnergy < 0d)
            ent.Comp.StoredEnergy = 0d;

        ent.Comp.StoredEnergy = Math.Min(ent.Comp.EnergyPerReward, ent.Comp.StoredEnergy + energy);
        UpdateInterlock(ent);
    }

    private void UpdateAppearance(Entity<FishBluespaceHarvesterComponent> ent)
    {
        _appearance.SetData(ent, FishBluespaceDeviceVisuals.Working, CanOperate(ent));
        _appearance.SetData(ent, FishBluespaceDeviceVisuals.Unstable,
            ent.Comp.Critical || _bluespaceBackground.GetLevel(ent) >= ent.Comp.IncidentLevel);
    }
}
