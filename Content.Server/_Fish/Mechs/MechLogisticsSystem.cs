using Content.Server.Atmos.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared._Fish.Mechs;
using Content.Shared._Fish.Mechs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Mechs;

public sealed class MechCabinRadioSystem : SharedMechCabinRadioSystem;

/// <summary>
/// Mech bay, pressure speed, wreckage salvage, phasing drain.
/// </summary>
public sealed class MechLogisticsSystem : EntitySystem
{
    [Dependency] private readonly SharedMechSystem _mech = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tools = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private static readonly ProtoId<ToolQualityPrototype> PryingQuality = "Prying";

    private TimeSpan _nextTick;
    private readonly HashSet<Entity<MechComponent>> _mechBuffer = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechComponent, MechBrokenEvent>(OnMechBroken);
        SubscribeLocalEvent<MechWreckageComponent, InteractUsingEvent>(OnWreckageInteract);
        SubscribeLocalEvent<MechWreckageComponent, MechWreckageSalvageDoAfterEvent>(OnWreckageSalvage);
        SubscribeLocalEvent<MechPressureSpeedComponent, RefreshMovementSpeedModifiersEvent>(OnPressureSpeed);
        SubscribeLocalEvent<MechPressureSpeedComponent, MapInitEvent>(OnPressureMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextTick)
            return;

        _nextTick = _timing.CurTime + TimeSpan.FromSeconds(1);

        var phasingQuery = EntityQueryEnumerator<MechPhasingComponent, MechComponent>();
        while (phasingQuery.MoveNext(out var uid, out var phasing, out var mech))
        {
            if (!phasing.Active)
                continue;

            if (!_mech.TryChangeEnergy(uid, -phasing.EnergyPerSecond, mech))
            {
                phasing.Active = false;
                Dirty(uid, phasing);
            }
        }

        var bayQuery = EntityQueryEnumerator<MechBayComponent, TransformComponent, ApcPowerReceiverComponent>();
        while (bayQuery.MoveNext(out var bayUid, out var bay, out var xform, out var power))
        {
            if (!power.Powered)
                continue;

            _mechBuffer.Clear();
            _lookup.GetEntitiesInRange(_xform.GetMapCoordinates(bayUid, xform), bay.Range, _mechBuffer);

            foreach (var mechEnt in _mechBuffer)
            {
                if (mechEnt.Comp.BatterySlot.ContainedEntity is not { } batteryUid)
                    continue;

                if (!TryComp(batteryUid, out BatteryComponent? bat))
                    continue;

                if (_battery.GetCharge((batteryUid, bat)) >= bat.MaxCharge)
                    continue;

                _battery.ChangeCharge((batteryUid, bat), bay.ChargeRate);
            }
        }
    }

    private void OnPressureMapInit(Entity<MechPressureSpeedComponent> ent, ref MapInitEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnPressureSpeed(Entity<MechPressureSpeedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var mixture = _atmos.GetContainingMixture(ent.Owner);
        var pressure = mixture?.Pressure ?? 0f;
        var mult = pressure < ent.Comp.LowPressureThreshold
            ? ent.Comp.LowPressureMultiplier
            : ent.Comp.HighPressureMultiplier;
        args.ModifySpeed(mult, mult);
    }

    private void OnMechBroken(Entity<MechComponent> ent, ref MechBrokenEvent args)
    {
        if (!TryComp(ent, out MechWreckageSpawnerComponent? spawner))
            return;

        var coords = Transform(ent).Coordinates;
        var wreck = Spawn(spawner.WreckagePrototype, coords);
        EnsureComp<MechWreckageComponent>(wreck);

        if (ent.Comp.BatterySlot.ContainedEntity is { } battery)
        {
            var container = _containers.EnsureContainer<Container>(wreck, "wreckage-loot");
            _containers.Insert(battery, container);
        }
    }

    private void OnWreckageInteract(Entity<MechWreckageComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tools.HasQuality(args.Used, PryingQuality))
            return;

        if (ent.Comp.SalvageLeft <= 0)
        {
            _popup.PopupEntity(Loc.GetString("mech-wreckage-empty"), ent, args.User);
            return;
        }

        args.Handled = true;
        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.SalvageDelay,
            new MechWreckageSalvageDoAfterEvent(), ent, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnWreckageSalvage(Entity<MechWreckageComponent> ent, ref MechWreckageSalvageDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        ent.Comp.SalvageLeft--;
        Dirty(ent);

        if (_containers.TryGetContainer(ent, "wreckage-loot", out var container) &&
            container.ContainedEntities.Count > 0)
        {
            var item = container.ContainedEntities[0];
            _containers.Remove(item, container);
            _xform.SetCoordinates(item, Transform(args.User).Coordinates);
            _popup.PopupEntity(Loc.GetString("mech-wreckage-salvaged"), ent, args.User);
        }
        else
        {
            Spawn("SheetSteel1", Transform(ent).Coordinates);
            _popup.PopupEntity(Loc.GetString("mech-wreckage-scrap"), ent, args.User);
        }

        if (ent.Comp.SalvageLeft <= 0)
            QueueDel(ent);
    }
}
