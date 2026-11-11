using Content.Shared.Access.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Interaction;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.DelayedOpen;

public sealed partial class DelayedOpenSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private AccessReaderSystem _access = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DelayedOpenComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<DelayedOpenComponent, DelayedOpenDoAfterEvent>(OnDoAfter);
    }

    private void OnActivate(EntityUid uid, DelayedOpenComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled || !component.Enabled)
            return;

        // If door is already open, do nothing or let default logic handle closing?
        // User said "before door begins to open".
        if (TryComp<DoorComponent>(uid, out var door) && door.State != DoorState.Closed && door.State != DoorState.Welded)
            return; // Already open or opening

        // Check Access
        if (!_access.IsAllowed(args.User, uid))
            return; // Let default access reader handle deny sound? Or should we handled it?
                    // Airlock system usually handles deny. If we don't set Handled = true, airlock might try to open and fail or play deny.
                    // But we want to BLOCK the immediate open.
                    // If we set Handled = true, we stop typical Airlock interaction.
                    // If access fails, we probably should let Airlock system play the deny sound.
                    // But Airlock system checks access too.
                    // If we return here, Airlock system runs. It checks access. Fails. Plays sound. Good.


        // If Access OK:
        // We want to PREVENT AirlockSystem from opening it immediately.
        // So we must handle the event.
        if (_access.IsAllowed(args.User, uid))
        {
             args.Handled = true;

             _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(component.Delay), new DelayedOpenDoAfterEvent(), uid, target: uid, used: uid)
             {
                 BreakOnMove = true,
                 BreakOnDamage = true,
                 NeedHand = false
             });
        }
    }

    private void OnDoAfter(EntityUid uid, DelayedOpenComponent component, DelayedOpenDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (args.Target is not { } target)
            return;

        if (_door.TryOpen(target))
            args.Handled = true;
    }
}

[Serializable, NetSerializable]
public sealed partial class DelayedOpenDoAfterEvent : SimpleDoAfterEvent { }
