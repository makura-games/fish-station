using Content.Server.Power.Components;
using Content.Shared._Fish.Xenobiology;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mind.Components;
using Content.Shared.NameIdentifier;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Temperature.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Xenobiology;

/// <summary>Owns safe specimen transport, powered extraction and read-only colony measurements.</summary>
public sealed partial class FishXenobiologyDeviceSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SatiationSystem _satiation = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private FishXenobiologySystem _xenobiology = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityQuery<FishXenobiologySlimeComponent> _slimeQuery = default!;
    [Dependency] private EntityQuery<TemperatureComponent> _temperatureQuery = default!;
    [Dependency] private EntityQuery<NameIdentifierComponent> _nameIdentifierQuery = default!;
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishXenobiologyCarrierComponent, ComponentInit>(OnCarrierInit);
        SubscribeLocalEvent<FishXenobiologyCarrierComponent, AfterInteractEvent>(OnCarrierInteract);
        SubscribeLocalEvent<FishXenobiologyCarrierComponent, UseInHandEvent>(OnCarrierUse);
        SubscribeLocalEvent<FishXenobiologyCarrierComponent, FishXenobiologyCaptureDoAfterEvent>(OnCaptured);
        SubscribeLocalEvent<FishXenobiologyCarrierComponent, ContainerIsInsertingAttemptEvent>(OnCarrierInsert);
        SubscribeLocalEvent<FishXenobiologyProcessorComponent, ComponentInit>(OnProcessorInit);
        SubscribeLocalEvent<FishXenobiologyProcessorComponent, ContainerIsInsertingAttemptEvent>(OnProcessorInsert);
        SubscribeLocalEvent<FishXenobiologyProcessorComponent, EntInsertedIntoContainerMessage>(OnProcessorInserted);
        SubscribeLocalEvent<FishXenobiologyProcessorComponent, EntRemovedFromContainerMessage>(OnProcessorRemoved);
        SubscribeLocalEvent<FishXenobiologyProcessorComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<FishXenobiologyConsoleComponent, PowerChangedEvent>(OnConsolePowerChanged);
        Subs.BuiEvents<FishXenobiologyProcessorComponent>(FishXenobiologyUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnProcessorOpened);
            subs.Event<FishXenobiologyProcessMessage>(OnProcess);
            subs.Event<FishXenobiologyEjectMessage>(OnEject);
        });
        Subs.BuiEvents<FishXenobiologyConsoleComponent>(FishXenobiologyUiKey.Key,
            subs => subs.Event<BoundUIOpenedEvent>(OnConsoleOpened));
    }

    private void OnCarrierInit(Entity<FishXenobiologyCarrierComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.Container);
    }

    private void OnProcessorInit(Entity<FishXenobiologyProcessorComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.Container);
    }

    private void OnCarrierInsert(Entity<FishXenobiologyCarrierComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID == ent.Comp.Container &&
            (!_slimeQuery.TryGetComponent(args.EntityUid, out var slime) || slime.Consumed ||
             !args.AssumeEmpty && args.Container.ContainedEntities.Count >= ent.Comp.Capacity))
            args.Cancel();
    }

    private void OnProcessorInsert(Entity<FishXenobiologyProcessorComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID == ent.Comp.Container && !IsHarvestable(args.EntityUid))
            args.Cancel();
    }

    private void OnProcessorInserted(Entity<FishXenobiologyProcessorComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.Container)
            UpdateProcessor(ent);
    }

    private void OnProcessorRemoved(Entity<FishXenobiologyProcessorComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.Container)
            return;

        RemCompDeferred<FishXenobiologyProcessingComponent>(ent);
        UpdateProcessor(ent);
    }

    private void OnCarrierInteract(Entity<FishXenobiologyCarrierComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (TryComp<FishXenobiologyProcessorComponent>(target, out var processor))
            args.Handled = TryTransfer(ent, args.User, (target, processor));
        else if (_slimeQuery.HasComp(target))
            args.Handled = TryCapture(ent, args.User, target);
    }

    /// <summary>Starts a cancellable capture with a held carrier and a reachable, uncontained slime.</summary>
    public bool TryCapture(Entity<FishXenobiologyCarrierComponent> carrier, EntityUid user, EntityUid target)
    {
        if (!CanCapture(carrier, user, target))
            return false;

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, carrier.Comp.CaptureDelay,
            new FishXenobiologyCaptureDoAfterEvent(), carrier, target: target, used: carrier)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 2f,
        });
    }

    /// <summary>Checks all capture conditions without changing specimen or container state.</summary>
    public bool CanCapture(Entity<FishXenobiologyCarrierComponent> carrier, EntityUid user, EntityUid target)
    {
        return !TerminatingOrDeleted(carrier) && !TerminatingOrDeleted(target) &&
               _hands.IsHolding(user, carrier) && _interaction.InRangeUnobstructed(user, target) &&
               _actionBlocker.CanInteract(user, target) &&
               !(TryComp<MindContainerComponent>(target, out var mind) && mind.HasMind) &&
               _slimeQuery.TryGetComponent(target, out var slime) && !slime.Consumed &&
               !_container.IsEntityInContainer(target) &&
               _container.TryGetContainer(carrier, carrier.Comp.Container, out var contents) &&
               contents.ContainedEntities.Count < carrier.Comp.Capacity;
    }

    private void OnCaptured(Entity<FishXenobiologyCarrierComponent> ent, ref FishXenobiologyCaptureDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !CanCapture(ent, args.User, target))
            return;

        if (_container.TryGetContainer(ent, ent.Comp.Container, out var contents))
        {
            args.Handled = _container.Insert(target, contents);
            if (args.Handled)
                _popup.PopupEntity(Loc.GetString("fish-xeno-captured"), ent, args.User);
        }
    }

    private void OnCarrierUse(Entity<FishXenobiologyCarrierComponent> ent, ref UseInHandEvent args)
    {
        if (!args.Handled)
            args.Handled = TryRelease(ent, args.User);
    }

    /// <summary>Releases one specimen at the holder's position through the normal container API.</summary>
    public bool TryRelease(Entity<FishXenobiologyCarrierComponent> carrier, EntityUid user)
    {
        if (!_hands.IsHolding(user, carrier) || !_actionBlocker.CanInteract(user, null) ||
            !_container.TryGetContainer(carrier, carrier.Comp.Container, out var contents) ||
            contents.ContainedEntities.Count == 0)
            return false;

        return _container.Remove(contents.ContainedEntities[0], contents,
            destination: Transform(user).Coordinates);
    }

    /// <summary>Transfers a corpse from the held carrier into the reached processor without dropping it.</summary>
    public bool TryTransfer(Entity<FishXenobiologyCarrierComponent> carrier, EntityUid user,
        Entity<FishXenobiologyProcessorComponent> processor)
    {
        if (!CanUse(processor, user) || !_hands.IsHolding(user, carrier) ||
            IsProcessing(processor) ||
            !_container.TryGetContainer(carrier, carrier.Comp.Container, out var source) ||
            source.ContainedEntities.Count == 0 ||
            !_container.TryGetContainer(processor, processor.Comp.Container, out var destination))
            return false;

        var corpse = source.ContainedEntities[0];
        return IsHarvestable(corpse) && _container.Insert(corpse, destination);
    }

    private bool IsHarvestable(EntityUid slime)
    {
        return _xenobiology.CanHarvest(slime);
    }

    private bool IsPowered(EntityUid machine)
    {
        return TryComp<ApcPowerReceiverComponent>(machine, out var receiver) && receiver.Powered;
    }

    private bool CanUse(EntityUid machine, EntityUid user)
    {
        return !TerminatingOrDeleted(machine) && !TerminatingOrDeleted(user) &&
               _actionBlocker.CanInteract(user, machine) &&
               _interaction.InRangeUnobstructed(user, machine);
    }

    private EntityUid? GetInput(Entity<FishXenobiologyProcessorComponent> processor)
    {
        if (_container.TryGetContainer(processor, processor.Comp.Container, out var contents) &&
            contents.ContainedEntities.Count > 0)
            return contents.ContainedEntities[0];

        return null;
    }

    /// <summary>Checks that a powered, idle processor contains an adult corpse which has not been harvested.</summary>
    public bool CanProcess(Entity<FishXenobiologyProcessorComponent> processor)
    {
        return IsPowered(processor) && !IsProcessing(processor) &&
               GetInput(processor) is { } input && IsHarvestable(input);
    }

    /// <summary>Starts powered processing; source eligibility is checked again on completion.</summary>
    public bool TryProcess(Entity<FishXenobiologyProcessorComponent> processor, EntityUid user)
    {
        if (!CanUse(processor, user) || !CanProcess(processor))
            return false;

        var processing = EnsureComp<FishXenobiologyProcessingComponent>(processor);
        processing.StartedAt = _timing.CurTime;
        processing.FinishAt = _timing.CurTime + TimeSpan.FromSeconds(Math.Max(1f, processor.Comp.Duration));
        UpdateProcessor(processor);
        return true;
    }

    private void OnProcess(Entity<FishXenobiologyProcessorComponent> ent, ref FishXenobiologyProcessMessage args)
    {
        if (!TryProcess(ent, args.Actor))
            UpdateProcessor(ent);
    }

    private void OnEject(Entity<FishXenobiologyProcessorComponent> ent, ref FishXenobiologyEjectMessage args)
    {
        if (!TryEject(ent, args.Actor))
            UpdateProcessor(ent);
    }

    /// <summary>Cancels processing and returns the corpse without spending its extraction yield.</summary>
    public bool TryEject(Entity<FishXenobiologyProcessorComponent> processor, EntityUid user)
    {
        if (!CanUse(processor, user) || GetInput(processor) is not { } input ||
            !_container.TryGetContainer(processor, processor.Comp.Container, out var contents))
            return false;

        RemComp<FishXenobiologyProcessingComponent>(processor);
        var removed = _container.Remove(input, contents, destination: Transform(user).Coordinates);
        UpdateProcessor(processor);
        return removed;
    }

    private void OnPowerChanged(Entity<FishXenobiologyProcessorComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            RemCompDeferred<FishXenobiologyProcessingComponent>(ent);
        UpdateProcessor(ent);
    }

    private void OnConsolePowerChanged(Entity<FishXenobiologyConsoleComponent> ent, ref PowerChangedEvent args)
    {
        UpdateConsole(ent);
    }

    private void OnProcessorOpened(Entity<FishXenobiologyProcessorComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateProcessor(ent);
    }

    private void OnConsoleOpened(Entity<FishXenobiologyConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateConsole(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextUpdate)
            return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);

        var processing = EntityQueryEnumerator<FishXenobiologyProcessingComponent, FishXenobiologyProcessorComponent>();
        while (processing.MoveNext(out var uid, out var active, out var processor))
        {
            if (!active.Running)
                continue;

            if (!IsPowered(uid) || GetInput((uid, processor)) is not { } input || !IsHarvestable(input))
            {
                RemCompDeferred<FishXenobiologyProcessingComponent>(uid);
                UpdateProcessor((uid, processor));
                continue;
            }

            if (_timing.CurTime < active.FinishAt)
                continue;

            RemCompDeferred<FishXenobiologyProcessingComponent>(uid);
            if (!_container.TryGetContainer(uid, processor.Container, out var contents) ||
                !_container.Remove(input, contents, destination: Transform(uid).Coordinates))
            {
                UpdateProcessor((uid, processor));
                continue;
            }

            if (!_xenobiology.TryHarvest(input, out var breed, out var yield))
            {
                UpdateProcessor((uid, processor));
                continue;
            }

            var extract = _prototype.Index(breed).Extract;
            for (var i = 0; i < yield; i++)
                Spawn(extract, Transform(uid).Coordinates);
            QueueDel(input);
            UpdateProcessor((uid, processor));
        }

        var processors = EntityQueryEnumerator<FishXenobiologyProcessorComponent>();
        while (processors.MoveNext(out var uid, out var processor))
        {
            if (_ui.IsUiOpen(uid, FishXenobiologyUiKey.Key))
                UpdateProcessor((uid, processor));
        }

        var consoles = EntityQueryEnumerator<FishXenobiologyConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            if (_ui.IsUiOpen(uid, FishXenobiologyUiKey.Key))
                UpdateConsole((uid, console));
        }
    }

    private bool IsProcessing(EntityUid processor)
    {
        return TryComp<FishXenobiologyProcessingComponent>(processor, out var processing) && processing.Running;
    }

    private void UpdateProcessor(Entity<FishXenobiologyProcessorComponent> ent)
    {
        var input = GetInput(ent);
        var harvestable = input is { } specimen && IsHarvestable(specimen);
        var state = new FishXenobiologyProcessorBoundUserInterfaceState
        {
            Powered = IsPowered(ent),
            CanProcess = CanProcess(ent),
            HasInput = input != null,
            CanEject = input != null,
            Duration = Math.Max(1f, ent.Comp.Duration),
        };
        if (input is { } uid && _slimeQuery.TryGetComponent(uid, out var slime))
        {
            state.Input = Measure(uid, slime);
            state.ExpectedYield = harvestable ? Math.Clamp(slime.ExtractYield, 1, 3) : 0;
        }

        if (state.Powered && harvestable &&
            TryComp<FishXenobiologyProcessingComponent>(ent, out var processing) && processing.Running)
        {
            state.Processing = true;
            state.Duration = Math.Max(1f, (float) (processing.FinishAt - processing.StartedAt).TotalSeconds);
            state.SecondsRemaining = Math.Clamp((float) (processing.FinishAt - _timing.CurTime).TotalSeconds,
                0f, state.Duration);
            state.Progress = Math.Clamp(1f - state.SecondsRemaining / state.Duration, 0f, 1f);
        }

        state.Status = !state.Powered ? FishXenobiologyProcessorStatus.NoPower
            : !state.HasInput ? FishXenobiologyProcessorStatus.Empty
            : !harvestable ? FishXenobiologyProcessorStatus.InvalidInput
            : state.Processing ? FishXenobiologyProcessorStatus.Processing
            : FishXenobiologyProcessorStatus.Ready;
        _ui.SetUiState(ent.Owner, FishXenobiologyUiKey.Key, state);
    }

    private void UpdateConsole(Entity<FishXenobiologyConsoleComponent> ent)
    {
        var state = new FishXenobiologyBoundUserInterfaceState { Powered = IsPowered(ent), Range = ent.Comp.Range };
        if (state.Powered)
        {
            var origin = Transform(ent);
            var position = _transform.GetWorldPosition(origin);
            var slimes = EntityQueryEnumerator<FishXenobiologySlimeComponent, TransformComponent>();
            while (slimes.MoveNext(out var uid, out var slime, out var transform))
            {
                if (!slime.Consumed && transform.GridUid == origin.GridUid && transform.MapUid == origin.MapUid &&
                    (_transform.GetWorldPosition(transform) - position).LengthSquared() <= ent.Comp.Range * ent.Comp.Range)
                    state.Specimens.Add(Measure(uid, slime));
            }
        }
        state.Specimens.Sort(static (first, second) => first.Entity.Id.CompareTo(second.Entity.Id));
        _ui.SetUiState(ent.Owner, FishXenobiologyUiKey.Key, state);
    }

    private FishXenobiologySpecimen Measure(EntityUid uid, FishXenobiologySlimeComponent slime)
    {
        var breed = _prototype.Index(slime.Breed);
        var metadata = MetaData(uid);
        return new FishXenobiologySpecimen
        {
            Entity = GetNetEntity(uid, metadata),
            Name = metadata.EntityName,
            Identifier = _nameIdentifierQuery.TryGetComponent(uid, out var identifier) ? identifier.FullIdentifier : string.Empty,
            Breed = breed.ID,
            Adult = slime.Adult,
            Dead = _mobState.IsDead(uid),
            Hostile = slime.Hostile,
            Suspended = slime.Suspended || _container.IsEntityInContainer(uid),
            Generation = slime.Generation,
            Growth = Math.Clamp(slime.Growth / Math.Max(1f, breed.GrowthDuration), 0f, 1f),
            Biomass = slime.Biomass,
            Hunger = TryComp<SatiationComponent>(uid, out var satiation)
                ? _satiation.GetValueOrNull((uid, satiation), SatiationSystem.Hunger) ?? 0f : 0f,
            Temperature = _temperatureQuery.TryGetComponent(uid, out var temperature) ? temperature.Temperature : 0f,
            MinimumTemperature = breed.MinimumTemperature - slime.ThermalTolerance,
            MaximumTemperature = breed.MaximumTemperature + slime.ThermalTolerance,
            GrowthBiomass = breed.GrowthBiomass,
            DivisionBiomass = breed.DivisionBiomass,
            MutationChance = slime.MutationChance,
            Offspring = slime.Offspring,
            ExtractYield = slime.ExtractYield,
            GrowthRate = slime.GrowthRate,
            Docility = slime.Docility,
            ThermalTolerance = slime.ThermalTolerance,
        };
    }
}
