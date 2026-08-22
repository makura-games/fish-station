using Content.Server.Mech.Systems;
using Content.Server.Popups;
using Content.Shared._Fish.BattleShuttles;
using Content.Shared._Fish.BattleShuttles.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Fish.BattleShuttles;

/// <summary>
/// Server-логика Battle Shuttle: установка модулей, ore scoop, lock buster.
/// </summary>
public sealed class BattleShuttleSystem : SharedBattleShuttleSystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMechSystem _mech = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private readonly HashSet<Entity<TagComponent>> _scoopBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BattleShuttleLockComponent, MapInitEvent>(OnLockMapInit);
        SubscribeLocalEvent<BattleShuttleComponent, MapInitEvent>(OnShuttleMapInit);
        SubscribeLocalEvent<BattleShuttleComponent, MoveEvent>(OnShuttleMove);
        SubscribeLocalEvent<BattleShuttleLockBusterComponent, AfterInteractEvent>(OnLockBusterAfterInteract, before: [typeof(MechEquipmentSystem)]);
        SubscribeLocalEvent<BattleShuttleLockBusterComponent, BattleShuttleLockBusterDoAfterEvent>(OnLockBusterDoAfter);
        SubscribeLocalEvent<BattleShuttleLockBusterComponent, ItemToggledEvent>(OnLockBusterToggled);
        SubscribeLocalEvent<BattleShuttleKeyComponent, AfterInteractEvent>(OnKeyAfterInteract, before: [typeof(MechEquipmentSystem)]);
        SubscribeLocalEvent<MechPilotComponent, ComponentStartup>(OnPilotStartup);

        // Directed на BattleShuttleModule (не MechEquipment) — иначе duplicate subscription с MechEquipmentSystem.
        // Ordering должен совпадать со всеми AfterInteract подписками этой системы.
        SubscribeLocalEvent<BattleShuttleModuleComponent, AfterInteractEvent>(OnModuleAfterInteract, before: [typeof(MechEquipmentSystem)]);
    }

    private void OnModuleAfterInteract(Entity<BattleShuttleModuleComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        if (!HasComp<BattleShuttleComponent>(args.Target.Value))
            return;

        if (TryBlockPlayerInstall(args.Target.Value, args.User, ent))
            args.Handled = true;
    }

    private void OnLockBusterToggled(Entity<BattleShuttleLockBusterComponent> ent, ref ItemToggledEvent args)
    {
        ent.Comp.Enabled = args.Activated;
        Dirty(ent);
    }

    private void OnLockMapInit(Entity<BattleShuttleLockComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.LockId != 0)
            return;

        ent.Comp.LockId = AssignLockId();
        Dirty(ent);
    }

    private void OnShuttleMapInit(Entity<BattleShuttleComponent> ent, ref MapInitEvent args)
    {
        // startingEquipment Mech MapInit уже вставлен; обновляем производное состояние.
        RefreshDerivedState(ent);
    }

    private void OnPilotStartup(Entity<MechPilotComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<BattleShuttleComponent>(ent.Comp.Mech, out var shuttle) || !shuttle.HasLock)
            return;

        UpdateLockAction(ent, (ent.Comp.Mech, shuttle), true);
    }

    private void OnShuttleMove(Entity<BattleShuttleComponent> ent, ref MoveEvent args)
    {
        if (!ent.Comp.HasActiveOreScoop)
            return;

        if (!TryGetOreScoopStorage(ent, out var storageEnt, out var scoop) || scoop == null)
            return;

        if (!TryComp(ent, out TransformComponent? xform))
            return;

        _scoopBuffer.Clear();
        _lookup.GetEntitiesInRange(new EntityCoordinates(ent, xform.LocalPosition), scoop.Range, _scoopBuffer);

        foreach (var nearby in _scoopBuffer)
        {
            if (nearby.Owner == ent.Owner || !_tag.HasTag(nearby.Comp, scoop.ScoopTag))
                continue;

            if (_storage.Insert(storageEnt, nearby.Owner, out _, user: ent))
                _popup.PopupEntity(Loc.GetString("battle-shuttle-ore-scoop"), ent, PopupType.Small);
        }
    }

    private void OnLockBusterAfterInteract(Entity<BattleShuttleLockBusterComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null || !ent.Comp.Enabled)
            return;

        if (!TryComp<BattleShuttleComponent>(args.Target.Value, out var shuttle) || !shuttle.HasLock)
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.BreakDelay,
            new BattleShuttleLockBusterDoAfterEvent(),
            ent,
            target: args.Target,
            used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnLockBusterDoAfter(Entity<BattleShuttleLockBusterComponent> ent, ref BattleShuttleLockBusterDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null)
            return;

        if (!TryComp<BattleShuttleComponent>(args.Args.Target.Value, out var shuttle))
            return;

        if (!TryGetEquipmentInSlot((args.Args.Target.Value, shuttle), "lock", out var lockEquip))
            return;

        args.Handled = true;
        _mech.RemoveEquipment(args.Args.Target.Value, lockEquip, forced: true);
        Del(lockEquip);
        SetUnlocked((args.Args.Target.Value, shuttle), true, args.Args.User);
        _popup.PopupEntity(Loc.GetString("battle-shuttle-lock-busted"), args.Args.Target.Value, PopupType.MediumCaution);
    }

    protected override int AssignLockId() => _random.Next(1000, 999999);
}
