using Content.Shared._Fish.Netrunner;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Emp;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Examine;
using Content.Shared.Implants.Components;
using Content.Shared.Inventory;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Overlays;
using Content.Shared.Radio.Components;
using Content.Shared.Stunnable;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.SurveillanceCamera.Components;
using Content.Server.SurveillanceCamera;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Content.Shared.Temperature.Components;
using Content.Shared.Temperature.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Netrunner;

/// <summary>Device-specific adapters; program transactions remain in NetrunnerSystem.</summary>
public sealed partial class NetrunnerEffectsSystem : EntitySystem
{
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private SharedPoweredLightSystem _light = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SurveillanceCameraSystem _camera = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private NetrunnerDisruptionSystem _disruption = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private SharedTemperatureSystem _temperature = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AirlockComponent, NetrunnerValidateHackEvent>(OnValidateAirlock);
        SubscribeLocalEvent<AirlockComponent, NetrunnerApplyHackEvent>(OnApplyAirlock);
        SubscribeLocalEvent<SurveillanceCameraComponent, NetrunnerValidateHackEvent>(OnValidateCamera);
        SubscribeLocalEvent<SurveillanceCameraComponent, NetrunnerApplyHackEvent>(OnApplyCamera);
        SubscribeLocalEvent<PoweredLightComponent, NetrunnerValidateHackEvent>(OnValidateLight);
        SubscribeLocalEvent<PoweredLightComponent, NetrunnerApplyHackEvent>(OnApplyLight);
        SubscribeLocalEvent<MobStateComponent, NetrunnerValidateHackEvent>(OnValidatePerson);
        SubscribeLocalEvent<MobStateComponent, NetrunnerApplyHackEvent>(OnApplyPerson);
        SubscribeLocalEvent<GunComponent, NetrunnerValidateHackEvent>(OnValidateWeapon);
        SubscribeLocalEvent<GunComponent, NetrunnerApplyHackEvent>(OnApplyWeapon);
        SubscribeLocalEvent<NetrunnerDisruptionComponent, SurveillanceCameraSetActiveAttemptEvent>(OnCameraActivate);
        SubscribeLocalEvent<NetrunnerDisruptionComponent, NetrunnerDisruptionEndedEvent>(OnDisruptionRemoved);
        SubscribeLocalEvent<NetrunnerAuditComponent, ExaminedEvent>(OnExamineAudit);
        SubscribeLocalEvent<NetrunnerAuditComponent, NetrunnerValidateHackEvent>(OnValidateAudit);
        SubscribeLocalEvent<NetrunnerAuditComponent, NetrunnerApplyHackEvent>(OnApplyAudit);
    }

    private void OnValidateAirlock(Entity<AirlockComponent> ent, ref NetrunnerValidateHackEvent args)
    {
        if (args.Ability.Kind == NetrunnerHackKind.LockAirlock)
        {
            args.Supported = true;
            args.Device = ent;
            args.Allowed = ent.Comp.Powered && !_door.IsBolted(ent)
                && TryComp<DoorComponent>(ent, out var closedDoor) && closedDoor.State == DoorState.Closed
                && !HasComp<NetrunnerDisruptionComponent>(ent);
            return;
        }
        if (args.Ability.Kind != NetrunnerHackKind.OpenAirlock)
            return;
        args.Supported = true;
        args.Device = ent;
        args.Allowed = ent.Comp.Powered && !_door.IsBolted(ent)
            && TryComp<DoorComponent>(ent, out var door) && door.State is DoorState.Closed or DoorState.Denying
            && _door.CanOpen(ent, user: null);
    }

    private void OnApplyAirlock(Entity<AirlockComponent> ent, ref NetrunnerApplyHackEvent args)
    {
        if (args.Ability.Kind == NetrunnerHackKind.OpenAirlock)
            args.Applied = _door.TryOpen(ent, user: null);
        else if (args.Ability.Kind == NetrunnerHackKind.LockAirlock)
        {
            Suppress(ent, args.Ability);
            args.Applied = true;
        }
    }

    private void OnValidateCamera(Entity<SurveillanceCameraComponent> ent, ref NetrunnerValidateHackEvent args)
    {
        if (args.Ability.Kind != NetrunnerHackKind.DisableCamera)
            return;
        args.Supported = true;
        args.Device = ent;
        args.Allowed = ent.Comp.Active && _power.IsPowered((ent.Owner, null))
            && !HasComp<NetrunnerDisruptionComponent>(ent);
    }

    private void OnApplyCamera(Entity<SurveillanceCameraComponent> ent, ref NetrunnerApplyHackEvent args)
    {
        if (args.Ability.Kind != NetrunnerHackKind.DisableCamera)
            return;
        Suppress(ent, args.Ability);
        _camera.SetActive(ent, false);
        args.Applied = true;
    }

    private void OnValidateLight(Entity<PoweredLightComponent> ent, ref NetrunnerValidateHackEvent args)
    {
        if (args.Ability.Kind != NetrunnerHackKind.DisableLight)
            return;
        args.Supported = true;
        args.Device = ent;
        args.Allowed = ent.Comp.On && _light.GetBulb(ent) != null
            && _power.IsPowered((ent.Owner, null)) && !HasComp<NetrunnerDisruptionComponent>(ent);
    }

    private void OnApplyLight(Entity<PoweredLightComponent> ent, ref NetrunnerApplyHackEvent args)
    {
        if (args.Ability.Kind != NetrunnerHackKind.DisableLight)
            return;
        Suppress(ent, args.Ability);
        _light.RefreshLight(ent);
        args.Applied = true;
    }

    private void OnValidatePerson(Entity<MobStateComponent> ent, ref NetrunnerValidateHackEvent args)
    {
        if (ent.Comp.CurrentState != MobState.Alive)
            return;
        if (args.Ability.Kind is NetrunnerHackKind.ShortCircuit or NetrunnerHackKind.MotorInterference
            or NetrunnerHackKind.OpticalGlitch or NetrunnerHackKind.LockWeapon or NetrunnerHackKind.GripShock
            or NetrunnerHackKind.NeuralStatic or NetrunnerHackKind.ThermalSurge or NetrunnerHackKind.BatteryDrain)
        {
            args.Supported = true;
            args.Allowed = CanAffectCombatTarget(ent, args.Ability.Kind, out var device);
            args.Device = device;
            return;
        }
        if (args.Ability.Kind == NetrunnerHackKind.NeuralOverload)
        {
            args.Supported = true;
            args.Device = ent;
            args.Allowed = HasMindshieldImplant(ent);
            return;
        }
        if (args.Ability.Kind == NetrunnerHackKind.DisableHeadset)
        {
            args.Supported = true;
            if (_inventory.TryGetSlotEntity(ent, "ears", out var headset)
                && TryComp<HeadsetComponent>(headset, out var component))
            {
                args.Device = headset;
                args.Allowed = component.IsEquipped && component.Enabled
                    && !HasComp<NetrunnerDisruptionComponent>(headset);
            }
            return;
        }
        if (args.Ability.Kind != NetrunnerHackKind.DisableSensors)
            return;
        args.Supported = true;
        var slots = _inventory.GetSlotEnumerator((ent.Owner, null), SlotFlags.All);
        while (slots.NextItem(out var item, out _))
        {
            if (!TryComp<SuitSensorComponent>(item, out var sensor) || sensor.User != ent.Owner
                || sensor.Mode == SuitSensorMode.SensorOff || HasComp<NetrunnerDisruptionComponent>(item))
                continue;
            args.Device = item;
            args.Allowed = true;
            return;
        }
    }

    private void OnApplyPerson(Entity<MobStateComponent> ent, ref NetrunnerApplyHackEvent args)
    {
        switch (args.Ability.Kind)
        {
            case NetrunnerHackKind.GripShock:
            case NetrunnerHackKind.NeuralStatic:
            case NetrunnerHackKind.ThermalSurge:
            case NetrunnerHackKind.BatteryDrain:
                args.Applied = TryApplyExpandedProgram(ent, args.Device, args.User, args.Ability);
                break;
            case NetrunnerHackKind.ShortCircuit:
            case NetrunnerHackKind.MotorInterference:
            case NetrunnerHackKind.OpticalGlitch:
            case NetrunnerHackKind.LockWeapon:
                if (!CanAffectCombatTarget(ent, args.Ability.Kind, out var device) || device != args.Device)
                    break;
                if (args.Ability.Kind == NetrunnerHackKind.ShortCircuit)
                    args.Applied = _damageable.TryChangeDamage((ent.Owner, null), args.Ability.Damage, origin: args.User);
                else
                {
                    Suppress(args.Device, args.Ability, ent);
                    args.Applied = true;
                }
                if (args.Applied)
                    _popup.PopupEntity(Loc.GetString("fish-netrunner-combat-warning"), ent, ent);
                break;
            case NetrunnerHackKind.NeuralOverload:
                args.Applied = HasMindshieldImplant(ent)
                    && _stun.TryUpdateParalyzeDuration(ent, args.Ability.Duration);
                break;
            case NetrunnerHackKind.DisableHeadset:
            case NetrunnerHackKind.DisableSensors:
                Suppress(args.Device, args.Ability);
                _popup.PopupEntity(Loc.GetString(args.Ability.Kind == NetrunnerHackKind.DisableHeadset
                    ? "fish-netrunner-headset-warning" : "fish-netrunner-sensors-warning"), ent, ent);
                args.Applied = true;
                break;
        }
    }

    private void OnValidateWeapon(Entity<GunComponent> ent, ref NetrunnerValidateHackEvent args)
    {
        if (args.Ability.Kind == NetrunnerHackKind.BatteryDrain)
        {
            args.Supported = true;
            args.Allowed = TryGetDrainBattery(ent, out var device);
            args.Device = device;
            return;
        }
        if (args.Ability.Kind != NetrunnerHackKind.LockWeapon)
            return;
        args.Supported = true;
        args.Device = ent;
        args.Allowed = CanLockWeapon(ent);
    }

    private void OnApplyWeapon(Entity<GunComponent> ent, ref NetrunnerApplyHackEvent args)
    {
        if (args.Ability.Kind == NetrunnerHackKind.BatteryDrain)
        {
            args.Applied = TryDrainBattery(ent, args.Device, args.Ability.ChargeFraction);
            return;
        }
        if (args.Ability.Kind != NetrunnerHackKind.LockWeapon || !CanLockWeapon(ent))
            return;
        Suppress(ent, args.Ability);
        args.Applied = true;
    }

    private bool CanLockWeapon(EntityUid weapon)
        => HasComp<GunComponent>(weapon) && HasComp<BatteryAmmoProviderComponent>(weapon)
            && !HasComp<NetrunnerDisruptionComponent>(weapon);

    private bool CanAffectCombatTarget(EntityUid target, NetrunnerHackKind kind, out EntityUid device)
    {
        device = target;
        if (!TryComp<MobStateComponent>(target, out var mob) || mob.CurrentState != MobState.Alive)
            return false;
        switch (kind)
        {
            case NetrunnerHackKind.ShortCircuit:
                return HasComp<BorgChassisComponent>(target) && HasComp<DamageableComponent>(target);
            case NetrunnerHackKind.MotorInterference:
                return (HasComp<BorgChassisComponent>(target) || HasMindshieldImplant(target))
                    && !HasComp<NetrunnerDisruptionComponent>(target);
            case NetrunnerHackKind.OpticalGlitch:
                if (!HasComp<BlindableComponent>(target))
                    return false;
                if (HasComp<BorgChassisComponent>(target))
                    return !HasComp<NetrunnerDisruptionComponent>(target);
                if (!_inventory.TryGetSlotEntity(target, "eyes", out var glasses)
                    || !(HasComp<ShowHealthBarsComponent>(glasses) || HasComp<ShowHealthIconsComponent>(glasses)
                        || HasComp<ShowJobIconsComponent>(glasses) || HasComp<ShowCriminalRecordIconsComponent>(glasses)
                        || HasComp<ShowMindShieldIconsComponent>(glasses)))
                    return false;
                device = glasses.Value;
                return !HasComp<NetrunnerDisruptionComponent>(device);
            case NetrunnerHackKind.LockWeapon:
                if (!_hands.TryGetActiveItem((target, null), out var gun) || !CanLockWeapon(gun.Value))
                    return false;
                device = gun.Value;
                return true;
            case NetrunnerHackKind.GripShock:
                return (HasComp<BorgChassisComponent>(target) || HasMindshieldImplant(target))
                    && _hands.TryGetActiveItem((target, null), out _);
            case NetrunnerHackKind.NeuralStatic:
                return (HasComp<BorgChassisComponent>(target) || HasMindshieldImplant(target))
                    && HasComp<StaminaComponent>(target);
            case NetrunnerHackKind.ThermalSurge:
                return (HasComp<BorgChassisComponent>(target) || HasMindshieldImplant(target))
                    && TryComp<TemperatureComponent>(target, out var temperature) && temperature.HeatCapacity > 0;
            case NetrunnerHackKind.BatteryDrain:
                return TryGetDrainBattery(target, out device);
            default:
                return false;
        }
    }

    private bool HasMindshieldImplant(EntityUid target)
    {
        if (!TryComp<ImplantedComponent>(target, out var implanted))
            return false;
        foreach (var implant in implanted.ImplantContainer.ContainedEntities)
        {
            if (HasComp<MindShieldImplantComponent>(implant)
                && TryComp<SubdermalImplantComponent>(implant, out var subdermal)
                && subdermal.ImplantedEntity == target)
                return true;
        }
        return false;
    }

    private void Suppress(EntityUid device, NetrunnerAbilityPrototype ability, EntityUid? affectedUser = null)
    {
        var component = AddComp<NetrunnerDisruptionComponent>(device);
        component.Kind = ability.Kind;
        component.EndsAt = _timing.CurTime + ability.Duration;
        component.AffectedUser = affectedUser;
        component.MovementMultiplier = ability.MovementMultiplier;
        Dirty(device, component);
        _disruption.RefreshEffect((device, component));
    }

    private void OnCameraActivate(Entity<NetrunnerDisruptionComponent> ent, ref SurveillanceCameraSetActiveAttemptEvent args)
    {
        if (ent.Comp.LifeStage < ComponentLifeStage.Stopping && ent.Comp.Kind == NetrunnerHackKind.DisableCamera)
            args.Cancelled = true;
    }

    private void OnDisruptionRemoved(Entity<NetrunnerDisruptionComponent> ent, ref NetrunnerDisruptionEndedEvent args)
    {
        if (Terminating(ent))
            return;
        if (ent.Comp.Kind == NetrunnerHackKind.DisableLight)
            _light.RefreshLight(ent);
        else if (ent.Comp.Kind == NetrunnerHackKind.DisableCamera
            && !HasComp<EmpDisabledComponent>(ent) && _power.IsPowered((ent.Owner, null)))
            _camera.SetActive(ent, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_nextUpdate > _timing.CurTime)
            return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.25);
        var query = EntityQueryEnumerator<NetrunnerDisruptionComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.EndsAt <= _timing.CurTime)
                RemCompDeferred<NetrunnerDisruptionComponent>(uid);
        }
        var audits = EntityQueryEnumerator<NetrunnerAuditComponent>();
        while (audits.MoveNext(out var uid, out var audit))
        {
            PruneAudit(audit);
            if (audit.Entries.Count == 0)
                RemCompDeferred<NetrunnerAuditComponent>(uid);
        }
    }

    private void OnExamineAudit(Entity<NetrunnerAuditComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;
        PruneAudit(ent.Comp);
        if (ent.Comp.Entries.Count == 0)
            return;
        args.PushMarkup(Loc.GetString("fish-netrunner-audit-header"));
        foreach (var entry in ent.Comp.Entries)
        {
            args.PushText(Loc.GetString("fish-netrunner-log-entry",
                ("time", entry.Time.ToString(@"mm\:ss")), ("program", Loc.GetString(entry.Kind.GetLocId())),
                ("target", entry.Target), ("result", Loc.GetString(entry.Success ? "fish-netrunner-log-success" : "fish-netrunner-log-failure"))));
        }
    }

    private bool TryApplyExpandedProgram(EntityUid target, EntityUid device, EntityUid user,
        NetrunnerAbilityPrototype ability)
    {
        if (!CanAffectCombatTarget(target, ability.Kind, out var current) || current != device)
            return false;
        switch (ability.Kind)
        {
            case NetrunnerHackKind.GripShock:
                return _hands.TryDrop((target, null), checkActionBlocker: false);
            case NetrunnerHackKind.NeuralStatic:
                _stamina.TakeStaminaDamage(target, ability.StaminaDamage, source: user, visual: false);
                return true;
            case NetrunnerHackKind.ThermalSurge:
                var body = Comp<TemperatureComponent>(target);
                return _temperature.ChangeHeat((target, body), ability.TemperatureIncrease * body.HeatCapacity) > 0;
            case NetrunnerHackKind.BatteryDrain:
                return TryDrainBattery(target, device, ability.ChargeFraction);
            default:
                return false;
        }
    }

    private bool TryGetDrainBattery(EntityUid target, out EntityUid device)
    {
        device = default;
        var source = target;
        if (!HasComp<BorgChassisComponent>(target) && !HasComp<BatteryAmmoProviderComponent>(target))
        {
            if (!_hands.TryGetActiveItem((target, null), out var gun) || !HasComp<BatteryAmmoProviderComponent>(gun))
                return false;
            source = gun.Value;
        }
        if (!_powerCell.TryGetBatteryFromSlotOrEntity((source, null), out var battery)
            || _battery.GetCharge(battery.Value.Owner).Charge <= 0)
            return false;
        device = battery.Value.Owner;
        return true;
    }

    private bool TryDrainBattery(EntityUid target, EntityUid device, float fraction)
    {
        if (!TryGetDrainBattery(target, out var battery) || battery != device)
            return false;
        var charge = _battery.GetCharge(device);
        return _battery.UseCharge((device, null), Math.Min(charge.Charge,
            charge.MaxCharge * Math.Clamp(fraction, 0, 0.25f))) != 0;
    }

    private void OnValidateAudit(Entity<NetrunnerAuditComponent> ent, ref NetrunnerValidateHackEvent args)
    {
        if (args.Ability.Kind != NetrunnerHackKind.EraseTrace)
            return;
        args.Supported = true;
        args.Device = ent;
        foreach (var entry in ent.Comp.Entries)
        {
            if (ent.Comp.Retention == null || entry.Time + ent.Comp.Retention > _timing.CurTime)
            {
                args.Allowed = true;
                break;
            }
        }
    }

    private void OnApplyAudit(Entity<NetrunnerAuditComponent> ent, ref NetrunnerApplyHackEvent args)
    {
        if (args.Ability.Kind != NetrunnerHackKind.EraseTrace)
            return;
        ent.Comp.Entries.Clear();
        args.Applied = true;
    }

    private void PruneAudit(NetrunnerAuditComponent audit)
    {
        if (audit.Retention is not { } retention)
            return;
        while (audit.Entries.Count > 0 && audit.Entries[0].Time + retention <= _timing.CurTime)
            audit.Entries.RemoveAt(0);
    }
}
