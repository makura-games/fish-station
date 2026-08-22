using System.Numerics;
using Content.Server._Fish.Leash.Components;
using Content.Shared.Alert;
using Content.Shared._Sunrise.Movement.Carrying;
using Content.Shared._Sunrise.Movement.Carrying.Slowdown;
using Content.Shared._Sunrise.Movement.Pulling;
using Content.Shared._Fish.Leash;
using Content.Shared._Fish.Leash.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Unit;
using Content.Shared.Disposal.Unit.Events;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Random;

namespace Content.Server._Fish.Leash.Systems;

public sealed partial class LeashSystem : EntitySystem
{
    private static readonly SpriteSpecifier.Rsi LeashVisualSprite =
        new(new ResPath("/Textures/_Fish/Objects/Fun/leash_line.rsi"), "line");
    private static readonly Vector2 LeashHolderOffset = Vector2.Zero;
    private static readonly Vector2 LeashWearerOffset = new(0f, 0.08f);

    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly HashSet<EntityUid> _allowedCollarUnequips = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CollarComponent, GotEquippedEvent>(OnCollarEquipped);
        SubscribeLocalEvent<CollarComponent, BeingUnequippedAttemptEvent>(OnCollarUnequipAttempt);
        SubscribeLocalEvent<CollarComponent, GotUnequippedEvent>(OnCollarUnequipped);
        SubscribeLocalEvent<CollarComponent, EntityTerminatingEvent>(OnCollarTerminating);
        SubscribeLocalEvent<CollarComponent, GetVerbsEvent<AlternativeVerb>>(OnCollarGetVerbs);

        SubscribeLocalEvent<LeashComponent, AfterInteractEvent>(OnLeashAfterInteract);
        SubscribeLocalEvent<LeashComponent, GotEquippedHandEvent>(OnLeashEquippedHand);
        SubscribeLocalEvent<LeashComponent, GotUnequippedHandEvent>(OnLeashUnequippedHand);
        SubscribeLocalEvent<LeashComponent, DroppedEvent>(OnLeashDropped);
        SubscribeLocalEvent<LeashComponent, EntInsertedIntoContainerMessage>(OnLeashInsertedIntoContainer);
        SubscribeLocalEvent<LeashComponent, EntityTerminatingEvent>(OnLeashTerminating);
        SubscribeLocalEvent<LeashComponent, UseInHandEvent>(OnLeashUseInHand);
        SubscribeLocalEvent<LeashComponent, ExaminedEvent>(OnLeashExamined);
        SubscribeLocalEvent<LeashComponent, GetVerbsEvent<AlternativeVerb>>(OnLeashGetVerbs);

        SubscribeLocalEvent<CollarWearerComponent, MoveInputEvent>(OnWearerMoveInput);
        SubscribeLocalEvent<CollarWearerComponent, GettingPickedUpAttemptEvent>(OnCollarWearerPickupAttempt);
        SubscribeLocalEvent<CollarWearerComponent, RemoveCollarAlertEvent>(OnRemoveCollarAlert);
        SubscribeLocalEvent<CollarWearerComponent, RemoveCollarDoAfterEvent>(OnRemoveCollarDoAfter);
        SubscribeLocalEvent<LeashHolderComponent, MoveInputEvent>(OnHolderMoveInput);
        SubscribeLocalEvent<CollarWearerComponent, DoAfterAttemptEvent<CarryDoAfterEvent>>(OnCarryDoAfterAttempt);
        SubscribeLocalEvent<LeashHolderComponent, DoAfterAttemptEvent<CarryDoAfterEvent>>(OnCarryDoAfterAttempt);
        SubscribeLocalEvent<DisposalUnitComponent, BeforeDisposalFlushEvent>(OnBeforeDisposalFlush);

        SubscribeLocalEvent<RemoveCollarDoAfterEvent>(OnRemoveCollarDoAfterVerb);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LeashComponent>();
        while (query.MoveNext(out var leashUid, out var leash))
        {
            UpdateLeashConstraint(leashUid, leash, frameTime);
        }
    }

    private void OnLeashUseInHand(EntityUid uid, LeashComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (component.Modes.Count == 0)
            return;

        args.Handled = true;

        component.CurrentModeIndex = (component.CurrentModeIndex + 1) % component.Modes.Count;
        var newDistance = component.Modes[component.CurrentModeIndex];
        component.MaximumDistance = newDistance;

        _popup.PopupEntity(Loc.GetString("leash-mode-changed", ("length", newDistance)), args.User, args.User);
    }

    private void OnLeashGetVerbs(EntityUid uid, LeashComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (component.Modes.Count < 2)
            return;

        var verb = new AlternativeVerb
        {
            Act = () =>
            {
                component.CurrentModeIndex = (component.CurrentModeIndex + 1) % component.Modes.Count;
                var newDistance = component.Modes[component.CurrentModeIndex];
                component.MaximumDistance = newDistance;
                _popup.PopupEntity(Loc.GetString("leash-mode-changed", ("length", newDistance)), args.User, args.User);
            },
            Text = Loc.GetString("leash-switch-mode-verb"),
            Priority = 1,
        };
        args.Verbs.Add(verb);
    }

    private void OnLeashExamined(EntityUid uid, LeashComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var currentLength = component.MaximumDistance;
        args.PushMarkup(Loc.GetString("leash-examine-length", ("length", currentLength)));
    }

    private void OnCollarEquipped(EntityUid uid, CollarComponent component, GotEquippedEvent args)
    {
        if (args.Slot != "neck")
            return;

        component.Wearer = args.Equipee;
        var wearer = EnsureComp<CollarWearerComponent>(args.Equipee);
        wearer.Collar = uid;
        _alerts.ShowAlert(args.Equipee, component.Alert);
    }

    private void OnCollarUnequipAttempt(EntityUid uid, CollarComponent component, BeingUnequippedAttemptEvent args)
    {
        if (args.Slot != "neck")
            return;

        if (_allowedCollarUnequips.Contains(uid))
            return;
    }

    private void OnCollarUnequipped(EntityUid uid, CollarComponent component, GotUnequippedEvent args)
    {
        var owner = args.Equipee;

        DetachLeashFromCollar(uid, component);

        if (TryComp<CollarWearerComponent>(owner, out var wearer) && wearer.Collar == uid)
        {
            RemCompDeferred<CollarWearerComponent>(owner);
        }

        component.Wearer = null;
        _alerts.ClearAlert(owner, component.Alert);
    }

    private void OnCollarTerminating(EntityUid uid, CollarComponent component, ref EntityTerminatingEvent args)
    {
        DetachLeashFromCollar(uid, component);

        if (component.Wearer is { Valid: true } wearer)
            _alerts.ClearAlert(wearer, component.Alert);
    }

    private void OnCollarGetVerbs(EntityUid uid, CollarComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (component.Wearer == null || component.Wearer == args.User)
            return;

        var verb = new AlternativeVerb
        {
            Act = () => TryRemoveCollarWithDoAfter(args.User, uid, component),
            Text = Loc.GetString("leash-remove-collar-verb"),
            Priority = 2,
        };
        args.Verbs.Add(verb);
    }

    private void TryRemoveCollarWithDoAfter(EntityUid user, EntityUid collarUid, CollarComponent collar)
    {
        if (collar.Wearer == null)
            return;

        var doAfter = new DoAfterArgs(EntityManager, user, collar.BreakoutTime, new RemoveCollarDoAfterEvent(), collar.Wearer.Value, target: collar.Wearer, used: collarUid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnDropItem = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            _popup.PopupEntity(Loc.GetString("leash-remove-collar-fail"), user, user, PopupType.SmallCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("leash-remove-collar-start"), user, user);
    }

    private void OnRemoveCollarDoAfterVerb(RemoveCollarDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var (user, target, used) = (args.Args.User, args.Args.Target, args.Args.Used);
        if (target == null || used == null)
            return;

        var collarUid = used.Value;
        if (!TryComp<CollarComponent>(collarUid, out var collar) || collar.Wearer != target)
            return;

        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("leash-remove-collar-fail"), target.Value, target.Value, PopupType.SmallCaution);
            return;
        }

        EntityUid? removedItem;
        _allowedCollarUnequips.Add(collarUid);
        try
        {
            if (!_inventory.TryUnequip(target.Value, user, "neck", out removedItem, checkDoafter: false))
            {
                _popup.PopupEntity(Loc.GetString("leash-remove-collar-fail"), target.Value, target.Value, PopupType.SmallCaution);
                return;
            }
        }
        finally
        {
            _allowedCollarUnequips.Remove(collarUid);
        }

        if (removedItem != null)
        {
            if (!_hands.TryPickupAnyHand(user, removedItem.Value))
            {
                Transform(removedItem.Value).AttachToGridOrMap();
            }
            _popup.PopupEntity(Loc.GetString("leash-remove-collar-success"), user, user);
        }
    }

    private void OnLeashAfterInteract(EntityUid uid, LeashComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        if (!TryResolveCollarTarget(args.Target.Value, out var collarUid, out var collar))
        {
            _popup.PopupEntity(Loc.GetString("leash-no-collar"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (collarUid == component.AttachedCollar)
        {
            DetachLeash(uid, component, popupUser: args.User);
            args.Handled = true;
            return;
        }

        if (collar.Wearer == null)
        {
            _popup.PopupEntity(Loc.GetString("leash-collar-must-be-worn"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (collar.Wearer == args.User)
        {
            _popup.PopupEntity(Loc.GetString("leash-cannot-attach-to-self"), args.User, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        AttachLeash(uid, component, collarUid, collar, args.User);
        args.Handled = true;
    }

    private void OnLeashEquippedHand(EntityUid uid, LeashComponent component, GotEquippedHandEvent args)
    {
        component.Holder = args.User;
        var holder = EnsureComp<LeashHolderComponent>(args.User);
        holder.Leashes.Add(uid);

        UpdateLeashVisuals(uid, component);
        TryStartLeashPull(uid, component);
    }

    private void OnLeashUnequippedHand(EntityUid uid, LeashComponent component, GotUnequippedHandEvent args)
    {
        if (component.Holder != args.User)
            return;

        RemoveHolderLeash(uid, args.User);
        component.Holder = null;
        UpdateLeashVisuals(uid, component);
    }

    private void OnLeashDropped(EntityUid uid, LeashComponent component, DroppedEvent args)
    {
        HandleLeashReleased(uid, component, args.User);
    }

    private void OnLeashInsertedIntoContainer(EntityUid uid, LeashComponent component, ref EntInsertedIntoContainerMessage args)
    {
        if (component.AttachedCollar != null)
            DetachLeash(uid, component);

        if (component.Holder != null)
        {
            StopLeashPull(uid, component);
            RemoveHolderLeash(uid, component.Holder);
            component.Holder = null;
        }

        UpdateLeashVisuals(uid, component);
    }

    private void OnLeashTerminating(EntityUid uid, LeashComponent component, ref EntityTerminatingEvent args)
    {
        DetachLeash(uid, component);
        RemoveHolderLeash(uid, component.Holder);
    }

    private void OnWearerMoveInput(EntityUid uid, CollarWearerComponent component, ref MoveInputEvent args)
    {
        if (!args.HasDirectionalMovement ||
            !TryGetLeash(uid, component, out _, out var leash) ||
            leash.Holder is not { Valid: true } holder)
        {
            return;
        }

        if (TryStopStretchingMovement(uid, holder, leash, args.Entity.Comp))
            return;

        if (_timing.CurTime < leash.NextChokeTime)
            return;

        var wearerCoords = _transform.GetMapCoordinates(uid);
        var holderCoords = _transform.GetMapCoordinates(holder);

        if (wearerCoords.MapId != holderCoords.MapId)
            return;

        var toHolder = holderCoords.Position - wearerCoords.Position;
        if (toHolder.LengthSquared() < leash.ChokeDistance * leash.ChokeDistance)
            return;

        var moveVector = GetMoveVector(args.Entity.Comp.HeldMoveButtons);
        if (moveVector.LengthSquared() <= 0f)
            return;

        var awayDot = Vector2.Dot(Vector2.Normalize(moveVector), Vector2.Normalize(toHolder));
        if (awayDot > -0.2f)
            return;
    }

    private void OnHolderMoveInput(EntityUid uid, LeashHolderComponent component, ref MoveInputEvent args)
    {
        foreach (var leashUid in component.Leashes)
        {
            if (!TryComp<LeashComponent>(leashUid, out var leash) ||
                !TryGetLeashWearer(leash, out var wearer) ||
                !TryStopStretchingMovement(uid, wearer, leash, args.Entity.Comp))
            {
                continue;
            }

            return;
        }
    }

    private void OnCollarWearerPickupAttempt(EntityUid uid, CollarWearerComponent component, GettingPickedUpAttemptEvent args)
    {
        if (!HasActiveLeash(uid))
            return;

        args.Cancel();
        if (args.ShowPopup)
            _popup.PopupEntity(Loc.GetString("leash-cannot-pick-up"), uid, args.User, PopupType.SmallCaution);
    }

    private void OnCarryDoAfterAttempt<TComponent>(EntityUid uid, TComponent component, DoAfterAttemptEvent<CarryDoAfterEvent> args)
        where TComponent : Component
    {
        if (!HasActiveLeash(uid) && !HasActiveLeash(args.DoAfter.Args.User))
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("leash-cannot-carry"), uid, args.DoAfter.Args.User, PopupType.SmallCaution);
    }

    private void OnBeforeDisposalFlush(EntityUid uid, DisposalUnitComponent component, BeforeDisposalFlushEvent args)
    {
        foreach (var contained in component.Container.ContainedEntities)
        {
            if (!HasActiveLeash(contained))
                continue;

            args.Cancel();
            return;
        }
    }

    private void OnRemoveCollarAlert(EntityUid uid, CollarWearerComponent component, ref RemoveCollarAlertEvent args)
    {
        if (args.Handled ||
            component.Collar is not { Valid: true } collarUid ||
            !TryComp<CollarComponent>(collarUid, out var collar) ||
            collar.Wearer != uid)
        {
            return;
        }

        TryStartRemoveCollarDoAfter(uid, collarUid, collar);
        args.Handled = true;
    }

    private void OnRemoveCollarDoAfter(EntityUid uid, CollarWearerComponent component, RemoveCollarDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (component.Collar is not { Valid: true } collarUid ||
            !TryComp<CollarComponent>(collarUid, out var collar) ||
            collar.Wearer != uid)
        {
            return;
        }

        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("leash-remove-collar-fail"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (collar.RemoveSuccessChance < 1f)
        {
            if (_random.NextFloat() > collar.RemoveSuccessChance)
            {
                _popup.PopupEntity(Loc.GetString("leash-remove-collar-fail-chance"), uid, uid, PopupType.SmallCaution);
                return;
            }
        }

        EntityUid? removedItem;
        _allowedCollarUnequips.Add(collarUid);
        try
        {
            if (!_inventory.TryUnequip(uid, uid, "neck", out removedItem, checkDoafter: false))
            {
                _popup.PopupEntity(Loc.GetString("leash-remove-collar-fail"), uid, uid, PopupType.SmallCaution);
                return;
            }
        }
        finally
        {
            _allowedCollarUnequips.Remove(collarUid);
        }

        if (removedItem != null)
            _hands.PickupOrDrop(uid, removedItem.Value);

        _popup.PopupEntity(Loc.GetString("leash-remove-collar-success"), uid, uid);
    }

    private void HandleLeashReleased(EntityUid uid, LeashComponent component, EntityUid user)
    {
        if (component.Holder != user)
            return;

        if (component.AttachedCollar != null)
            DetachLeash(uid, component);

        StopLeashPull(uid, component);
        RemoveHolderLeash(uid, component.Holder);
        component.Holder = null;
        UpdateLeashVisuals(uid, component);
    }

    private void AttachLeash(EntityUid leashUid, LeashComponent leash, EntityUid collarUid, CollarComponent collar, EntityUid user)
    {
        DetachLeash(leashUid, leash);

        if (collar.AttachedLeash is { Valid: true } otherLeashUid &&
            otherLeashUid != leashUid &&
            TryComp<LeashComponent>(otherLeashUid, out var otherLeash))
        {
            DetachLeash(otherLeashUid, otherLeash);
        }

        leash.AttachedCollar = collarUid;
        collar.AttachedLeash = leashUid;
        leash.NextChokeTime = _timing.CurTime;

        if (collar.Wearer is { Valid: true } wearer)
        {
            _popup.PopupEntity(Loc.GetString("leash-attached-user"), user, user);
            _popup.PopupEntity(Loc.GetString("leash-attached-target"), wearer, wearer);
        }

        UpdateLeashVisuals(leashUid, leash);
        TryStartLeashPull(leashUid, leash);
    }

    private void DetachLeash(EntityUid leashUid, LeashComponent leash, EntityUid? popupUser = null)
    {
        var oldCollarUid = leash.AttachedCollar;
        if (oldCollarUid == null)
            return;

        StopLeashPull(leashUid, leash);

        if (TryComp<CollarComponent>(oldCollarUid, out var oldCollar) &&
            oldCollar.AttachedLeash == leashUid)
        {
            oldCollar.AttachedLeash = null;

            if (popupUser != null &&
                oldCollar.Wearer is { Valid: true } wearer)
            {
                _popup.PopupEntity(Loc.GetString("leash-detached-user"), popupUser.Value, popupUser.Value);
                _popup.PopupEntity(Loc.GetString("leash-detached-target"), wearer, wearer);
            }
        }

        leash.AttachedCollar = null;
        UpdateLeashVisuals(leashUid, leash);
    }

    private void DetachLeashFromCollar(EntityUid collarUid, CollarComponent collar)
    {
        if (collar.AttachedLeash is not { Valid: true } leashUid ||
            !TryComp<LeashComponent>(leashUid, out var leash))
        {
            collar.AttachedLeash = null;
            return;
        }

        if (leash.AttachedCollar == collarUid)
            DetachLeash(leashUid, leash);

        collar.AttachedLeash = null;
    }

    private void TryStartLeashPull(EntityUid leashUid, LeashComponent leash)
    {
        if (leash.Holder is not { Valid: true } holder ||
            leash.AttachedCollar is not { Valid: true } collarUid ||
            !TryComp<CollarComponent>(collarUid, out var collar) ||
            collar.Wearer is not { Valid: true } wearer ||
            holder == wearer)
        {
            return;
        }
    }

    private void StopLeashPull(EntityUid leashUid, LeashComponent leash)
    {
        if (leash.Holder is { Valid: true } holder)
            _physics.WakeBody(holder);

        if (leash.AttachedCollar is { Valid: true } collarUid &&
            TryComp<CollarComponent>(collarUid, out var collar) &&
            collar.Wearer is { Valid: true } wearer)
        {
            _physics.WakeBody(wearer);
        }
    }

    private void RemoveHolderLeash(EntityUid leashUid, EntityUid? holderUid)
    {
        if (holderUid is not { Valid: true } holder ||
            !TryComp<LeashHolderComponent>(holder, out var holderComp))
        {
            return;
        }

        holderComp.Leashes.Remove(leashUid);
        if (holderComp.Leashes.Count == 0)
            RemCompDeferred<LeashHolderComponent>(holder);
    }

    private bool TryResolveCollarTarget(EntityUid target, out EntityUid collarUid, out CollarComponent collar)
    {
        collarUid = default;
        collar = default!;

        if (TryComp<CollarComponent>(target, out CollarComponent? directCollar) &&
            directCollar.Wearer != null)
        {
            collar = directCollar;
            collarUid = target;
            return true;
        }

        if (!_inventory.TryGetSlotEntity(target, "neck", out EntityUid? neckItem) ||
            neckItem == null ||
            !TryComp<CollarComponent>(neckItem.Value, out CollarComponent? slotCollar))
        {
            return false;
        }

        collar = slotCollar;
        collarUid = neckItem.Value;
        return true;
    }

    private bool TryGetLeash(EntityUid wearer, CollarWearerComponent wearerComp, out EntityUid leashUid, out LeashComponent leash)
    {
        leashUid = default;
        leash = default!;

        if (wearerComp.Collar is not { Valid: true } collarUid ||
            !TryComp<CollarComponent>(collarUid, out var collar) ||
            collar.Wearer != wearer ||
            collar.AttachedLeash is not { Valid: true } attachedLeash ||
            !TryComp<LeashComponent>(attachedLeash, out LeashComponent? leashComp))
        {
            return false;
        }

        leash = leashComp;
        leashUid = attachedLeash;
        return true;
    }

    private bool TryGetLeashWearer(LeashComponent leash, out EntityUid wearer)
    {
        wearer = default;

        if (leash.AttachedCollar is not { Valid: true } collarUid ||
            !TryComp<CollarComponent>(collarUid, out var collar) ||
            collar.Wearer is not { Valid: true } collarWearer)
        {
            return false;
        }

        wearer = collarWearer;
        return true;
    }

    private bool HasActiveLeash(EntityUid uid)
    {
        if (TryComp<CollarWearerComponent>(uid, out var wearer) &&
            wearer.Collar is { Valid: true } collarUid &&
            TryComp<CollarComponent>(collarUid, out var collar) &&
            collar.AttachedLeash is { Valid: true })
        {
            return true;
        }

        if (!TryComp<LeashHolderComponent>(uid, out var holder))
            return false;

        foreach (var leashUid in holder.Leashes)
        {
            if (TryComp<LeashComponent>(leashUid, out var leash) &&
                leash.Holder == uid &&
                leash.AttachedCollar is { Valid: true })
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateLeashVisuals(EntityUid leashUid, LeashComponent leash)
    {
        if (leash.Holder is { Valid: true } holder &&
            leash.AttachedCollar is { Valid: true } collarUid &&
            TryComp<CollarComponent>(collarUid, out var collar) &&
            collar.Wearer is { Valid: true } wearer &&
            holder != wearer)
        {
            var visuals = EnsureComp<JointVisualsComponent>(leashUid);
            visuals.Sprite = LeashVisualSprite;
            visuals.Target = wearer;
            visuals.OffsetA = LeashHolderOffset;
            visuals.OffsetB = LeashWearerOffset;
            Dirty(leashUid, visuals);
            return;
        }

        RemCompDeferred<JointVisualsComponent>(leashUid);
    }

    private static Vector2 GetMoveVector(MoveButtons buttons)
    {
        var x = 0;
        x -= (buttons & MoveButtons.Left) != 0 ? 1 : 0;
        x += (buttons & MoveButtons.Right) != 0 ? 1 : 0;

        var y = 0;
        y -= (buttons & MoveButtons.Down) != 0 ? 1 : 0;
        y += (buttons & MoveButtons.Up) != 0 ? 1 : 0;

        var vector = new Vector2(x, y);
        return vector.LengthSquared() > 0f ? Vector2.Normalize(vector) : Vector2.Zero;
    }

    private bool TryStartRemoveCollarDoAfter(EntityUid user, EntityUid collarUid, CollarComponent collar)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, collar.BreakoutTime, new RemoveCollarDoAfterEvent(), user, target: user, used: collarUid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnDropItem = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("leash-remove-collar-start"), user, user);
        return true;
    }

    private bool TryStopStretchingMovement(EntityUid mover, EntityUid anchor, LeashComponent leash, InputMoverComponent moverComp)
    {
        if (!IsMovingAwayPastLimit(mover, anchor, moverComp.HeldMoveButtons, leash.MaximumDistance))
            return false;

        moverComp.CurTickWalkMovement = Vector2.Zero;
        moverComp.CurTickSprintMovement = Vector2.Zero;
        Dirty(mover, moverComp);
        _physics.WakeBody(mover);
        return true;
    }

    private bool IsMovingAwayPastLimit(EntityUid mover, EntityUid anchor, MoveButtons buttons, float maxDistance)
    {
        var moverCoords = _transform.GetMapCoordinates(mover);
        var anchorCoords = _transform.GetMapCoordinates(anchor);

        if (moverCoords.MapId != anchorCoords.MapId)
            return false;

        var away = moverCoords.Position - anchorCoords.Position;
        if (away.LengthSquared() < maxDistance * maxDistance)
            return false;

        var moveVector = GetMoveVector(buttons);
        if (moveVector.LengthSquared() <= 0f || away.LengthSquared() <= 0.001f)
            return false;

        return Vector2.Dot(Vector2.Normalize(moveVector), Vector2.Normalize(away)) > 0.2f;
    }

    private void UpdateLeashConstraint(EntityUid leashUid, LeashComponent leash, float frameTime)
    {
        if (leash.Holder is not { Valid: true } holder ||
            leash.AttachedCollar is not { Valid: true } collarUid ||
            !TryComp<CollarComponent>(collarUid, out var collar) ||
            collar.Wearer is not { Valid: true } wearer ||
            holder == wearer)
        {
            return;
        }

        var wearerCoords = _transform.GetMapCoordinates(wearer);
        var holderCoords = _transform.GetMapCoordinates(holder);
        if (wearerCoords.MapId != holderCoords.MapId)
        {
            DetachLeash(leashUid, leash);
            return;
        }

        var delta = holderCoords.Position - wearerCoords.Position;
        var distance = delta.Length();
        if (distance <= leash.MaxTensionDistance || distance <= 0.001f)
            return;

        if (!TryComp<PhysicsComponent>(wearer, out var wearerBody))
            return;

        var direction = delta / distance;
        var excess = distance - leash.MaxTensionDistance;
        var impulse = direction * (leash.PullForce * MathF.Max(1f, excess * 3f) * wearerBody.Mass * frameTime);

        _physics.WakeBody(wearer);
        _physics.ApplyLinearImpulse(wearer, impulse, body: wearerBody);

        if (distance >= leash.MaximumDistance &&
            TryComp<PhysicsComponent>(holder, out var holderBody))
        {
            var holderImpulse = -direction * (leash.PullForce * MathF.Max(1f, (distance - leash.MaximumDistance) * 3f) * holderBody.Mass * frameTime);
            _physics.WakeBody(holder);
            _physics.ApplyLinearImpulse(holder, holderImpulse, body: holderBody);

            if (TryComp<InputMoverComponent>(holder, out var holderMover))
                TryStopStretchingMovement(holder, wearer, leash, holderMover);
        }

        if (distance < leash.ChokeDistance || _timing.CurTime < leash.NextChokeTime)
            return;

        leash.NextChokeTime = _timing.CurTime + leash.ChokeCooldown;
        _damageable.TryChangeDamage(wearer, leash.ChokeDamage, ignoreResistances: true, interruptsDoAfters: false, origin: holder);
        _popup.PopupEntity(Loc.GetString("leash-choking"), wearer, wearer, PopupType.SmallCaution);
    }
}