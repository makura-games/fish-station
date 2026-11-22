using Content.Shared.Eye;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Radio;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Doors;
using Robust.Shared.GameObjects;

namespace Content.Shared._Fish.Netrunner;

/// <summary>Suppresses device operation without changing persistent user settings.</summary>
public sealed partial class NetrunnerDisruptionSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private SharedEyeSystem _eye = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NetrunnerDisruptionComponent, RadioSendAttemptEvent>(OnSend);
        SubscribeLocalEvent<NetrunnerDisruptionComponent, RadioReceiveAttemptEvent>(OnReceive);
        SubscribeLocalEvent<NetrunnerDisruptionComponent, GetPoweredLightEnabledEvent>(OnLight);
        SubscribeLocalEvent<NetrunnerDisruptionComponent, SuitSensorStateAttemptEvent>(OnSensor);
        SubscribeLocalEvent<NetrunnerDisruptionComponent, ShotAttemptedEvent>(OnShoot);
        SubscribeLocalEvent<NetrunnerDisruptionComponent, BeforeDoorOpenedEvent>(OnDoorOpen);
        SubscribeLocalEvent<NetrunnerDisruptionComponent, CanSeeAttemptEvent>(OnSee);
        SubscribeLocalEvent<NetrunnerDisruptionComponent, InventoryRelayedEvent<CanSeeAttemptEvent>>(OnInventorySee);
        SubscribeLocalEvent<NetrunnerDisruptionComponent, RefreshMovementSpeedModifiersEvent>(OnSpeed);
        SubscribeLocalEvent<NetrunnerDisruptionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<NetrunnerDisruptionComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<NetrunnerDisruptionComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<NetrunnerDisruptionComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<StationAiHeldComponent, ComponentStartup>(OnStationAiVisibilityRefresh);
        SubscribeLocalEvent<StationAiHeldComponent, ComponentShutdown>(OnStationAiVisibilityRefresh);
        SubscribeLocalEvent<StationAiHeldComponent, GetVisMaskEvent>(OnStationAiVisMask);
        SubscribeLocalEvent<NetrunnerRemoteEyeComponent, GetVisMaskEvent>(OnRemoteEyeVisMask);
        SubscribeLocalEvent<NetrunnerUserComponent, GetVisMaskEvent>(OnNetrunnerUserVisMask);
    }

    private void OnStationAiVisibilityRefresh<TEvent>(Entity<StationAiHeldComponent> ent, ref TEvent args)
    {
        _eye.RefreshVisibilityMask(ent.Owner);
    }

    private void OnStationAiVisMask(Entity<StationAiHeldComponent> ent, ref GetVisMaskEvent args)
    {
        if (ent.Comp.LifeStage < ComponentLifeStage.Stopping)
            args.VisibilityMask |= (int) VisibilityFlags.Netrunner;
    }

    private void OnRemoteEyeVisMask(Entity<NetrunnerRemoteEyeComponent> ent, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int) VisibilityFlags.Netrunner;
    }

    private void OnNetrunnerUserVisMask(Entity<NetrunnerUserComponent> ent, ref GetVisMaskEvent args)
    {
        if (ent.Comp.RemoteEye != null)
            args.VisibilityMask |= (int) VisibilityFlags.Netrunner;
    }

    private void OnShoot(Entity<NetrunnerDisruptionComponent> ent, ref ShotAttemptedEvent args)
    {
        if (ent.Comp.LifeStage < ComponentLifeStage.Stopping && ent.Comp.Kind == NetrunnerHackKind.LockWeapon)
        {
            args.Message = Loc.GetString("fish-netrunner-weapon-jammed");
            args.Cancel();
        }
    }

    private void OnDoorOpen(Entity<NetrunnerDisruptionComponent> ent, ref BeforeDoorOpenedEvent args)
    {
        if (ent.Comp.LifeStage < ComponentLifeStage.Stopping && ent.Comp.Kind == NetrunnerHackKind.LockAirlock)
            args.Cancel();
    }

    private void OnSee(Entity<NetrunnerDisruptionComponent> ent, ref CanSeeAttemptEvent args)
    {
        if (ent.Comp.LifeStage < ComponentLifeStage.Stopping && ent.Comp.Kind == NetrunnerHackKind.OpticalGlitch)
            args.Cancel();
    }

    private void OnInventorySee(Entity<NetrunnerDisruptionComponent> ent, ref InventoryRelayedEvent<CanSeeAttemptEvent> args)
    {
        if (ent.Comp.LifeStage < ComponentLifeStage.Stopping && ent.Comp.Kind == NetrunnerHackKind.OpticalGlitch)
            args.Args.Cancel();
    }

    private void OnSpeed(Entity<NetrunnerDisruptionComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.LifeStage < ComponentLifeStage.Stopping && ent.Comp.Kind == NetrunnerHackKind.MotorInterference)
            args.ModifySpeed(ent.Comp.MovementMultiplier);
    }

    private void OnShutdown(Entity<NetrunnerDisruptionComponent> ent, ref ComponentShutdown args)
    {
        RefreshEffect(ent);
        // Fish-edit: У ComponentShutdown один обработчик на компонент; сервер уведомляем отдельным событием.
        var ended = new NetrunnerDisruptionEndedEvent();
        RaiseLocalEvent(ent, ref ended);
    }
    private void OnState(Entity<NetrunnerDisruptionComponent> ent, ref AfterAutoHandleStateEvent args) => RefreshEffect(ent);

    private void OnEquipped(Entity<NetrunnerDisruptionComponent> ent, ref GotEquippedEvent args)
    {
        if (ent.Comp.Kind != NetrunnerHackKind.OpticalGlitch)
            return;
        ent.Comp.AffectedUser = args.EquipTarget;
        Dirty(ent);
        RefreshEffect(ent);
    }

    private void OnUnequipped(Entity<NetrunnerDisruptionComponent> ent, ref GotUnequippedEvent args) => RefreshEffect(ent);

    /// <summary>Recalculates body state without changing persistent movement or vision settings.</summary>
    public void RefreshEffect(Entity<NetrunnerDisruptionComponent> ent)
    {
        if (ent.Comp.AffectedUser is not { } user || Terminating(user))
            return;
        if (ent.Comp.Kind == NetrunnerHackKind.MotorInterference)
            _movementSpeedModifier.RefreshMovementSpeedModifiers(user);
        else if (ent.Comp.Kind == NetrunnerHackKind.OpticalGlitch)
            _blindable.UpdateIsBlind((user, null));
    }

    private void OnSend(Entity<NetrunnerDisruptionComponent> ent, ref RadioSendAttemptEvent args)
    {
        if (ent.Comp.LifeStage < ComponentLifeStage.Stopping && ent.Comp.Kind == NetrunnerHackKind.DisableHeadset)
            args.Cancelled = true;
    }

    private void OnReceive(Entity<NetrunnerDisruptionComponent> ent, ref RadioReceiveAttemptEvent args)
    {
        if (ent.Comp.LifeStage < ComponentLifeStage.Stopping && ent.Comp.Kind == NetrunnerHackKind.DisableHeadset)
            args.Cancelled = true;
    }

    private void OnLight(Entity<NetrunnerDisruptionComponent> ent, ref GetPoweredLightEnabledEvent args)
    {
        if (ent.Comp.LifeStage < ComponentLifeStage.Stopping && ent.Comp.Kind == NetrunnerHackKind.DisableLight)
            args.Enabled = false;
    }

    private void OnSensor(Entity<NetrunnerDisruptionComponent> ent, ref SuitSensorStateAttemptEvent args)
    {
        if (ent.Comp.LifeStage < ComponentLifeStage.Stopping && ent.Comp.Kind == NetrunnerHackKind.DisableSensors)
            args.Cancelled = true;
    }
}
