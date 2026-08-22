using Content.Shared._Fish.Mechs.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Fish.Mechs;

/// <summary>
/// Шасси-способности: overload, defence, thrusters, smoke, strafe + выдача actions пилоту.
/// </summary>
public abstract class SharedMechChassisAbilitySystem : EntitySystem
{
    [Dependency] protected readonly SharedActionsSystem Actions = default!;
    [Dependency] protected readonly SharedMechSystem Mech = default!;
    [Dependency] protected readonly MovementSpeedModifierSystem Movement = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] protected readonly ActionBlockerSystem Blocker = default!;
    [Dependency] protected readonly DamageableSystem Damageable = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly SharedContentEyeSystem ContentEye = default!;
    [Dependency] protected readonly SharedPhysicsSystem Physics = default!;
    [Dependency] protected readonly INetManager Net = default!;

    private EntityQuery<MechComponent> _mechQuery;

    public override void Initialize()
    {
        base.Initialize();
        _mechQuery = GetEntityQuery<MechComponent>();

        // Fish: hook из SharedMechSystem.SetupUser (не ComponentStartup — conflict с BattleShuttle).
        SubscribeLocalEvent<MechComponent, MechPilotReadyEvent>(OnPilotReady);

        SubscribeLocalEvent<MechOverloadComponent, MechToggleOverloadEvent>(OnToggleOverload);
        SubscribeLocalEvent<MechOverloadComponent, RefreshMovementSpeedModifiersEvent>(OnOverloadSpeed);
        SubscribeLocalEvent<MechOverloadComponent, MoveInputEvent>(OnOverloadMove);

        SubscribeLocalEvent<MechDefenceModeComponent, MechToggleDefenceEvent>(OnToggleDefence);
        SubscribeLocalEvent<MechDefenceModeComponent, UpdateCanMoveEvent>(OnDefenceCanMove);

        SubscribeLocalEvent<MechThrustersComponent, MechToggleThrustersEvent>(OnToggleThrusters);
        SubscribeLocalEvent<MechThrustersComponent, CanWeightlessMoveEvent>(OnThrustersWeightless);

        SubscribeLocalEvent<MechSmokeComponent, MechLaunchSmokeEvent>(OnLaunchSmoke);

        SubscribeLocalEvent<MechStrafeComponent, MechToggleStrafeEvent>(OnToggleStrafe);
        SubscribeLocalEvent<MechStrafeComponent, MoveInputEvent>(OnStrafeMove);

        SubscribeLocalEvent<MechZoomComponent, MechToggleZoomEvent>(OnToggleZoom);
        SubscribeLocalEvent<MechZoomComponent, UpdateCanMoveEvent>(OnZoomCanMove);

        SubscribeLocalEvent<MechPhasingComponent, MechTogglePhasingEvent>(OnTogglePhasing);
        SubscribeLocalEvent<MechPhasingComponent, RefreshMovementSpeedModifiersEvent>(OnPhasingSpeed);

        SubscribeLocalEvent<MechDamtypeCycleComponent, MechCycleDamtypeEvent>(OnCycleDamtype);
    }

    private void OnPilotReady(Entity<MechComponent> ent, ref MechPilotReadyEvent args)
    {
        if (Net.IsClient)
            return;

        var pilot = args.Pilot;
        var mech = ent.Owner;

        if (TryComp(mech, out MechOverloadComponent? overload))
            Actions.AddAction(pilot, ref overload.ToggleActionEntity, overload.ToggleAction, mech);

        if (TryComp(mech, out MechDefenceModeComponent? defence))
            Actions.AddAction(pilot, ref defence.ToggleActionEntity, defence.ToggleAction, mech);

        if (TryComp(mech, out MechThrustersComponent? thrusters))
            Actions.AddAction(pilot, ref thrusters.ToggleActionEntity, thrusters.ToggleAction, mech);

        if (TryComp(mech, out MechSmokeComponent? smoke))
            Actions.AddAction(pilot, ref smoke.LaunchActionEntity, smoke.LaunchAction, mech);

        if (TryComp(mech, out MechStrafeComponent? strafe))
            Actions.AddAction(pilot, ref strafe.ToggleActionEntity, strafe.ToggleAction, mech);

        if (TryComp(mech, out MechDualEquipmentComponent? dual))
            Actions.AddAction(pilot, ref dual.SwapActionEntity, dual.SwapAction, mech);

        if (TryComp(mech, out MechDnaLockComponent? dna))
        {
            Actions.AddAction(pilot, ref dna.SetDnaActionEntity, dna.SetDnaAction, mech);
            Actions.AddAction(pilot, ref dna.ClearDnaActionEntity, dna.ClearDnaAction, mech);
        }

        if (TryComp(mech, out MechZoomComponent? zoom))
            Actions.AddAction(pilot, ref zoom.ToggleActionEntity, zoom.ToggleAction, mech);

        if (TryComp(mech, out MechPhasingComponent? phasing))
            Actions.AddAction(pilot, ref phasing.ToggleActionEntity, phasing.ToggleAction, mech);

        if (TryComp(mech, out MechDamtypeCycleComponent? damtype))
            Actions.AddAction(pilot, ref damtype.CycleActionEntity, damtype.CycleAction, mech);

        if (TryComp(mech, out MechCabinAtmosComponent? cabin))
            Actions.AddAction(pilot, ref cabin.ToggleActionEntity, cabin.ToggleAction, mech);

        if (TryComp(mech, out MechRadioComponent? radio))
        {
            Actions.AddAction(pilot, ref radio.ToggleMicActionEntity, radio.ToggleMicAction, mech);
            Actions.AddAction(pilot, ref radio.ToggleSpeakerActionEntity, radio.ToggleSpeakerAction, mech);
        }
    }

    private void OnToggleOverload(Entity<MechOverloadComponent> ent, ref MechToggleOverloadEvent args)
    {
        if (args.Handled || !_mechQuery.TryGetComponent(ent, out var mech))
            return;

        args.Handled = true;

        if (!ent.Comp.Active)
        {
            var ratio = mech.MaxIntegrity <= 0 ? 1f : (mech.Integrity / mech.MaxIntegrity).Float();
            if (ratio >= ent.Comp.MaxDamageRatio)
            {
                Popup.PopupClient(Loc.GetString("mech-overload-too-damaged"), ent, args.Performer);
                return;
            }
        }

        ent.Comp.Active = !ent.Comp.Active;
        Dirty(ent);
        Actions.SetToggled(ent.Comp.ToggleActionEntity, ent.Comp.Active);
        Movement.RefreshMovementSpeedModifiers(ent);
        Popup.PopupClient(
            Loc.GetString(ent.Comp.Active ? "mech-overload-on" : "mech-overload-off"),
            ent,
            args.Performer);
    }

    private void OnOverloadSpeed(Entity<MechOverloadComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Active)
            args.ModifySpeed(ent.Comp.SpeedMultiplier, ent.Comp.SpeedMultiplier);
    }

    private void OnOverloadMove(Entity<MechOverloadComponent> ent, ref MoveInputEvent args)
    {
        if (!ent.Comp.Active || !args.HasDirectionalMovement)
            return;

        if (!_mechQuery.TryGetComponent(ent, out var mech))
            return;

        if (!Mech.TryChangeEnergy(ent, -ent.Comp.EnergyPerStep, mech))
        {
            ent.Comp.Active = false;
            Dirty(ent);
            Actions.SetToggled(ent.Comp.ToggleActionEntity, false);
            Movement.RefreshMovementSpeedModifiers(ent);
            return;
        }

        var ratio = mech.MaxIntegrity <= 0 ? 1f : (mech.Integrity / mech.MaxIntegrity).Float();
        if (ratio >= ent.Comp.MaxDamageRatio)
        {
            ent.Comp.Active = false;
            Dirty(ent);
            Actions.SetToggled(ent.Comp.ToggleActionEntity, false);
            Movement.RefreshMovementSpeedModifiers(ent);
            return;
        }

        if (Net.IsServer && ent.Comp.SelfDamagePerStep > 0f)
        {
            var selfDamage = new DamageSpecifier();
            selfDamage.DamageDict.Add("Blunt", ent.Comp.SelfDamagePerStep);
            Damageable.TryChangeDamage(ent.Owner, selfDamage);
        }
    }

    private void OnToggleDefence(Entity<MechDefenceModeComponent> ent, ref MechToggleDefenceEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Active = !ent.Comp.Active;
        Dirty(ent);
        Actions.SetToggled(ent.Comp.ToggleActionEntity, ent.Comp.Active);
        // Сопротивление урону применяется в SharedMechFacingArmorSystem при Active.
        Blocker.UpdateCanMove(ent);
        Popup.PopupClient(
            Loc.GetString(ent.Comp.Active ? "mech-defence-on" : "mech-defence-off"),
            ent,
            args.Performer);
    }

    private void OnDefenceCanMove(Entity<MechDefenceModeComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.Active)
            args.Cancel();
    }

    private void OnToggleThrusters(Entity<MechThrustersComponent> ent, ref MechToggleThrustersEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Active = !ent.Comp.Active;
        Dirty(ent);
        Actions.SetToggled(ent.Comp.ToggleActionEntity, ent.Comp.Active);
        // Движение в невесомости — через CanWeightlessMoveEvent, без EnsureComp (prediction-safe).
        Popup.PopupClient(
            Loc.GetString(ent.Comp.Active ? "mech-thrusters-on" : "mech-thrusters-off"),
            ent,
            args.Performer);
    }

    private void OnThrustersWeightless(Entity<MechThrustersComponent> ent, ref CanWeightlessMoveEvent args)
    {
        if (ent.Comp.Active)
            args.CanMove = true;
    }

    private void OnLaunchSmoke(Entity<MechSmokeComponent> ent, ref MechLaunchSmokeEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Charges <= 0)
        {
            Popup.PopupClient(Loc.GetString("mech-smoke-empty"), ent, args.Performer);
            return;
        }

        if (Timing.CurTime < ent.Comp.NextReady)
        {
            Popup.PopupClient(Loc.GetString("mech-smoke-cooldown"), ent, args.Performer);
            return;
        }

        if (!LaunchSmokeEffect(ent))
            return;

        ent.Comp.Charges--;
        ent.Comp.NextReady = Timing.CurTime + ent.Comp.Cooldown;
        Dirty(ent);
        Popup.PopupClient(Loc.GetString("mech-smoke-launched", ("charges", ent.Comp.Charges)), ent, args.Performer);
    }

    /// <summary>
    /// Сервер спавнит дым; по умолчанию false (только server override).
    /// </summary>
    protected virtual bool LaunchSmokeEffect(Entity<MechSmokeComponent> ent)
    {
        return false;
    }

    private void OnToggleStrafe(Entity<MechStrafeComponent> ent, ref MechToggleStrafeEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Active = !ent.Comp.Active;
        Dirty(ent);
        Actions.SetToggled(ent.Comp.ToggleActionEntity, ent.Comp.Active);
        Popup.PopupClient(
            Loc.GetString(ent.Comp.Active ? "mech-strafe-on" : "mech-strafe-off"),
            ent,
            args.Performer);
    }

    private void OnStrafeMove(Entity<MechStrafeComponent> ent, ref MoveInputEvent args)
    {
        if (!ent.Comp.Active || !args.HasDirectionalMovement)
            return;

        if (!_mechQuery.TryGetComponent(ent, out var mech))
            return;

        // Сохраняем facing: не даём input повернуть корпус (strafe = боковое смещение).
        // Расход энергии за шаг ввода.
        var cost = ent.Comp.EnergyPerStep;
        Mech.TryChangeEnergy(ent, -cost, mech);
    }

    private void OnToggleZoom(Entity<MechZoomComponent> ent, ref MechToggleZoomEvent args)
    {
        if (args.Handled || !_mechQuery.TryGetComponent(ent, out var mech))
            return;

        args.Handled = true;
        ent.Comp.Active = !ent.Comp.Active;
        Dirty(ent);
        Actions.SetToggled(ent.Comp.ToggleActionEntity, ent.Comp.Active);
        Blocker.UpdateCanMove(ent);

        if (!Timing.ApplyingState &&
            mech.PilotSlot.ContainedEntity is { } pilot &&
            TryComp(pilot, out ContentEyeComponent? eye))
        {
            if (ent.Comp.Active)
                ContentEye.SetZoom(pilot, ent.Comp.Zoom, ignoreLimits: true, eye);
            else
                ContentEye.ResetZoom(pilot, eye);
        }

        Popup.PopupClient(
            Loc.GetString(ent.Comp.Active ? "mech-zoom-on" : "mech-zoom-off"),
            ent,
            args.Performer);
    }

    private void OnZoomCanMove(Entity<MechZoomComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.Active)
            args.Cancel();
    }

    private void OnTogglePhasing(Entity<MechPhasingComponent> ent, ref MechTogglePhasingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Active = !ent.Comp.Active;
        Dirty(ent);
        Actions.SetToggled(ent.Comp.ToggleActionEntity, ent.Comp.Active);
        Movement.RefreshMovementSpeedModifiers(ent);
        if (!Timing.ApplyingState)
            ApplyPhasingFixtures(ent, ent.Comp.Active);
        Popup.PopupClient(
            Loc.GetString(ent.Comp.Active ? "mech-phasing-on" : "mech-phasing-off"),
            ent,
            args.Performer);
    }

    private void OnPhasingSpeed(Entity<MechPhasingComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Active)
            args.ModifySpeed(ent.Comp.SpeedMultiplier, ent.Comp.SpeedMultiplier);
    }

    private void ApplyPhasingFixtures(EntityUid uid, bool phasing)
    {
        if (!TryComp(uid, out FixturesComponent? fixtures))
            return;

        foreach (var fixture in fixtures.Fixtures.Values)
            Physics.SetHard(uid, fixture, !phasing, fixtures);
    }

    private void OnCycleDamtype(Entity<MechDamtypeCycleComponent> ent, ref MechCycleDamtypeEvent args)
    {
        if (args.Handled || ent.Comp.DamageTypes.Count == 0)
            return;

        args.Handled = true;
        ent.Comp.ModeIndex = (ent.Comp.ModeIndex + 1) % ent.Comp.DamageTypes.Count;
        Dirty(ent);

        var type = ent.Comp.DamageTypes[ent.Comp.ModeIndex];
        if (TryComp(ent, out MeleeWeaponComponent? melee))
        {
            melee.Damage = new DamageSpecifier();
            melee.Damage.DamageDict[type] = ent.Comp.DamageAmount;
            Dirty(ent.Owner, melee);
        }

        Popup.PopupClient(Loc.GetString("mech-damtype-cycled", ("type", type)), ent, args.Performer);
    }
}
