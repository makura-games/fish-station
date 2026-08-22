using Content.Shared._Fish.BattleShuttles.Components;
using Content.Shared.Actions;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.DragDrop;
using Content.Shared.Interaction;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Wires;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.BattleShuttles;

/// <summary>
/// Shared-логика специализации Mech → Battle Shuttle (люк, замок, слоты, модификаторы).
/// </summary>
public abstract class SharedBattleShuttleSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedMechSystem _mech = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedToolSystem _tools = default!;

    private static readonly ProtoId<ToolQualityPrototype> PryingQuality = "Prying";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BattleShuttleComponent, CanDropTargetEvent>(OnCanDropTarget);
        SubscribeLocalEvent<BattleShuttleComponent, MechEntryEvent>(OnMechEntry);
        // Лом: при закрытой wires panel — люк; при открытой — батарея Mech (мы не перехватываем).
        SubscribeLocalEvent<BattleShuttleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<BattleShuttleComponent, ToggleBattleShuttleLockEvent>(OnToggleLockAction);
        SubscribeLocalEvent<BattleShuttleComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);

        SubscribeLocalEvent<MechEquipmentComponent, MechEquipmentInsertedEvent>(OnEquipmentInserted);
        SubscribeLocalEvent<MechEquipmentComponent, MechEquipmentRemovedEvent>(OnEquipmentRemoved);

        SubscribeLocalEvent<BattleShuttleLockComponent, InteractUsingEvent>(OnLockInteractUsing);
    }

    private void OnCanDropTarget(Entity<BattleShuttleComponent> ent, ref CanDropTargetEvent args)
    {
        if (ent.Comp.Unlocked)
            return;

        args.CanDrop = false;
        args.Handled = true;
        _popup.PopupClient(Loc.GetString("battle-shuttle-locked"), ent, args.User);
    }

    private void OnMechEntry(Entity<BattleShuttleComponent> ent, ref MechEntryEvent args)
    {
        if (args.Handled || args.Cancelled || ent.Comp.Unlocked)
            return;

        args.Handled = true;
        _popup.PopupClient(Loc.GetString("battle-shuttle-locked"), ent, args.User);
    }

    private void OnInteractUsing(Entity<BattleShuttleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_tools.HasQuality(args.Used, PryingQuality))
            return;

        // Открытая панель проводов — зона обслуживания батареи Mech, люк не трогаем.
        if (TryComp<WiresPanelComponent>(ent, out var panel) && panel.Open)
            return;

        if (!TryComp<MechComponent>(ent, out var mech) || mech.Broken)
            return;

        if (ent.Comp.HasLock && !ent.Comp.Unlocked && !ent.Comp.HatchOpen)
        {
            _popup.PopupClient(Loc.GetString("battle-shuttle-hatch-locked"), ent, args.User);
            args.Handled = true;
            return;
        }

        SetHatchOpen(ent, !ent.Comp.HatchOpen);
        args.Handled = true;
    }

    private void OnToggleLockAction(Entity<BattleShuttleComponent> ent, ref ToggleBattleShuttleLockEvent args)
    {
        if (args.Handled || !ent.Comp.HasLock)
            return;

        args.Handled = true;
        SetUnlocked(ent, !ent.Comp.Unlocked, args.Performer);
    }

    private void OnRefreshMovementSpeed(Entity<BattleShuttleComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<MechComponent>(ent, out var mech))
            return;

        var walk = 1f;
        var sprint = 1f;

        foreach (var equipment in mech.EquipmentContainer.ContainedEntities)
        {
            if (!TryComp<BattleShuttleModuleComponent>(equipment, out var module))
                continue;

            walk *= module.WalkSpeedModifier;
            sprint *= module.SprintSpeedModifier;
        }

        args.ModifySpeed(walk, sprint);
    }

    private void OnEquipmentInserted(Entity<MechEquipmentComponent> ent, ref MechEquipmentInsertedEvent args)
    {
        if (!TryComp<BattleShuttleComponent>(args.Mech, out var shuttle))
            return;

        if (!TryComp<BattleShuttleModuleComponent>(ent, out var module))
            return;

        var shuttleEnt = (args.Mech, shuttle);

        if (!IsModuleCompatible(shuttleEnt, module))
        {
            _popup.PopupClient(Loc.GetString("battle-shuttle-module-incompatible"), args.Mech);
            _mech.RemoveEquipment(args.Mech, ent, forced: true);
            return;
        }

        if (!string.IsNullOrEmpty(module.Slot) &&
            TryGetEquipmentInSlot(shuttleEnt, module.Slot, out var existing) &&
            existing != ent.Owner)
        {
            _popup.PopupClient(
                Loc.GetString("battle-shuttle-slot-filled", ("slot", module.Slot)),
                args.Mech);
            _mech.RemoveEquipment(args.Mech, ent, forced: true);
            return;
        }

        RefreshDerivedState(shuttleEnt);
    }

    private void OnEquipmentRemoved(Entity<MechEquipmentComponent> ent, ref MechEquipmentRemovedEvent args)
    {
        if (!TryComp<BattleShuttleComponent>(args.Mech, out var shuttle))
            return;

        RefreshDerivedState((args.Mech, shuttle));
    }

    protected void OnKeyAfterInteract(Entity<BattleShuttleKeyComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        if (!TryComp<BattleShuttleComponent>(args.Target.Value, out var shuttle) || !shuttle.HasLock)
            return;

        if (ent.Comp.LockId == null)
        {
            _popup.PopupClient(Loc.GetString("battle-shuttle-key-blank"), ent, args.User);
            return;
        }

        if (ent.Comp.LockId != shuttle.LockId)
        {
            _popup.PopupClient(Loc.GetString("battle-shuttle-key-wrong"), args.Target.Value, args.User);
            return;
        }

        args.Handled = true;
        SetUnlocked((args.Target.Value, shuttle), !shuttle.Unlocked, args.User);
    }

    private void OnLockInteractUsing(Entity<BattleShuttleLockComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<BattleShuttleKeyComponent>(args.Used, out var key))
            return;

        // Импринт только пока замок не установлен в шаттл.
        if (TryComp<MechEquipmentComponent>(ent, out var equip) && equip.EquipmentOwner != null)
            return;

        ent.Comp.LockId = ent.Comp.LockId == 0 ? AssignLockId() : ent.Comp.LockId;
        key.LockId = ent.Comp.LockId;
        Dirty(ent);
        Dirty(args.Used, key);

        _popup.PopupClient(Loc.GetString("battle-shuttle-key-imprinted", ("id", ent.Comp.LockId)), args.Used, args.User);
        args.Handled = true;
    }

    /// <summary>
    /// Блокирует установку модуля игроком (вызывается с Server before MechEquipmentSystem).
    /// </summary>
    public bool TryBlockPlayerInstall(EntityUid mech, EntityUid user, EntityUid equipment)
    {
        if (!TryComp<BattleShuttleComponent>(mech, out var shuttle))
            return false;

        if (shuttle.RequireOpenHatchForInstall && !shuttle.HatchOpen)
        {
            _popup.PopupClient(Loc.GetString("battle-shuttle-hatch-closed"), mech, user);
            return true;
        }

        if (!TryComp<BattleShuttleModuleComponent>(equipment, out var module))
            return false;

        if (!IsModuleCompatible((mech, shuttle), module))
        {
            _popup.PopupClient(Loc.GetString("battle-shuttle-module-incompatible"), mech, user);
            return true;
        }

        if (!string.IsNullOrEmpty(module.Slot) && TryGetEquipmentInSlot((mech, shuttle), module.Slot, out _))
        {
            _popup.PopupClient(
                Loc.GetString("battle-shuttle-slot-filled", ("slot", module.Slot)),
                mech,
                user);
            return true;
        }

        return false;
    }

    public bool IsModuleCompatible(Entity<BattleShuttleComponent> shuttle, BattleShuttleModuleComponent module)
    {
        if (module.CompatibleShuttleTags.Count == 0)
            return true;

        foreach (var required in module.CompatibleShuttleTags)
        {
            foreach (var owned in shuttle.Comp.ClassTags)
            {
                if (owned == required)
                    return true;
            }
        }

        return false;
    }

    public void SetHatchOpen(Entity<BattleShuttleComponent> ent, bool open)
    {
        ent.Comp.HatchOpen = open;
        Dirty(ent);
    }

    public void SetUnlocked(Entity<BattleShuttleComponent> ent, bool unlocked, EntityUid? user = null)
    {
        ent.Comp.Unlocked = unlocked;
        Dirty(ent);

        if (user == null)
            return;

        _popup.PopupClient(
            unlocked
                ? Loc.GetString("battle-shuttle-unlocked")
                : Loc.GetString("battle-shuttle-locked-toggle"),
            ent,
            user);
    }

    public void RefreshDerivedState(Entity<BattleShuttleComponent> ent)
    {
        RefreshLockState(ent);
        RefreshPassengerCapacity(ent);
        RefreshOreScoopCache(ent);
        _movement.RefreshMovementSpeedModifiers(ent);
        ApplyMassModifiers(ent);
    }

    public void RefreshLockState(Entity<BattleShuttleComponent> ent)
    {
        if (!TryComp<MechComponent>(ent, out var mech))
            return;

        var hasLock = false;
        var lockId = 0;

        foreach (var equipment in mech.EquipmentContainer.ContainedEntities)
        {
            if (!TryComp<BattleShuttleLockComponent>(equipment, out var lockComp))
                continue;

            hasLock = true;
            lockId = lockComp.LockId;
            break;
        }

        ent.Comp.HasLock = hasLock;
        ent.Comp.LockId = lockId;
        Dirty(ent);

        if (mech.PilotSlot.ContainedEntity is { } pilot)
            UpdateLockAction(pilot, ent, hasLock);
    }

    public void RefreshPassengerCapacity(Entity<BattleShuttleComponent> ent)
    {
        if (!TryComp<MechComponent>(ent, out var mech))
            return;

        var passengers = ent.Comp.BasePassengerCapacity;
        foreach (var equipment in mech.EquipmentContainer.ContainedEntities)
        {
            if (TryComp<BattleShuttleModuleComponent>(equipment, out var module))
                passengers += module.OccupantMod;
        }

        ent.Comp.MaxPassengers = passengers;
        Dirty(ent);

        if (!TryComp<StrapComponent>(ent, out var strap))
            return;

        _buckle.StrapSetEnabled(ent, passengers > 0, strap);
    }

    public void RefreshOreScoopCache(Entity<BattleShuttleComponent> ent)
    {
        ent.Comp.HasActiveOreScoop = false;

        if (!TryComp<MechComponent>(ent, out var mech))
            return;

        foreach (var equipment in mech.EquipmentContainer.ContainedEntities)
        {
            if (!HasComp<BattleShuttleOreScoopComponent>(equipment))
                continue;

            if (!HasComp<Content.Shared.Storage.StorageComponent>(equipment))
                continue;

            ent.Comp.HasActiveOreScoop = true;
            break;
        }
    }

    public void ApplyMassModifiers(Entity<BattleShuttleComponent> ent)
    {
        if (!TryComp<MechComponent>(ent, out var mech) || !TryComp<FixturesComponent>(ent, out var fixtures))
            return;

        var massMod = 1f;
        foreach (var equipment in mech.EquipmentContainer.ContainedEntities)
        {
            if (TryComp<BattleShuttleModuleComponent>(equipment, out var module))
                massMod *= module.MassModifier;
        }

        var density = ent.Comp.BaseFixtureDensity * massMod;
        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            if (!fixture.Hard)
                continue;

            _physics.SetDensity(ent, id, fixture, density, false, fixtures);
            break;
        }
    }

    public void UpdateLockAction(EntityUid pilot, Entity<BattleShuttleComponent> shuttle, bool hasLock)
    {
        if (hasLock)
        {
            _actions.AddAction(pilot, ref shuttle.Comp.ToggleLockActionEntity, shuttle.Comp.ToggleLockAction, shuttle);
            return;
        }

        if (shuttle.Comp.ToggleLockActionEntity == null)
            return;

        _actions.RemoveAction(pilot, shuttle.Comp.ToggleLockActionEntity.Value);
        shuttle.Comp.ToggleLockActionEntity = null;
        Dirty(shuttle);
    }

    public bool TryGetEquipmentInSlot(Entity<BattleShuttleComponent> shuttle, string slot, out EntityUid equipment)
    {
        equipment = default;

        if (string.IsNullOrEmpty(slot) || !TryComp<MechComponent>(shuttle, out var mech))
            return false;

        foreach (var ent in mech.EquipmentContainer.ContainedEntities)
        {
            if (!TryComp<BattleShuttleModuleComponent>(ent, out var module) || module.Slot != slot)
                continue;

            equipment = ent;
            return true;
        }

        return false;
    }

    public bool TryGetOreScoopStorage(
        EntityUid shuttle,
        out EntityUid storageEntity,
        out BattleShuttleOreScoopComponent? scoop)
    {
        storageEntity = default;
        scoop = null;

        if (!TryComp<MechComponent>(shuttle, out var mech))
            return false;

        foreach (var ent in mech.EquipmentContainer.ContainedEntities)
        {
            if (!TryComp<BattleShuttleOreScoopComponent>(ent, out scoop))
                continue;

            if (!HasComp<Content.Shared.Storage.StorageComponent>(ent))
                continue;

            storageEntity = ent;
            return true;
        }

        return false;
    }

    protected virtual int AssignLockId() => 1;
}
