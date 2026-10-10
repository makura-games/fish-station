using Content.Shared.Mech.Components;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared._Fish.Mech;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._Fish.Mech;

public sealed partial class MechMassScannerSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechComponent, MechOpenMassScannerEvent>(OnOpenMassScanner);
    }

    private void OnOpenMassScanner(EntityUid uid, MechComponent component, MechOpenMassScannerEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryOpenMassScanner((uid, component), args.Performer);
    }

    private bool TryOpenMassScanner(Entity<MechComponent> ent, EntityUid performer)
    {
        if (!CanOpenMassScanner(ent, performer))
            return false;

        var pilot = ent.Comp.PilotSlot.ContainedEntity!.Value;
        var actor = Comp<ActorComponent>(pilot);
        return DoOpenMassScanner(ent.Owner, actor);
    }

    private bool CanOpenMassScanner(Entity<MechComponent> ent, EntityUid performer)
    {
        if (ent.Comp.PilotSlot.ContainedEntity != performer)
            return false;

        if (!TryComp<ActorComponent>(performer, out _))
            return false;

        return _ui.HasUi(ent.Owner, RadarConsoleUiKey.Key);
    }

    private bool DoOpenMassScanner(EntityUid uid, ActorComponent actor)
    {
        return _ui.TryToggleUi(uid, RadarConsoleUiKey.Key, actor.PlayerSession);
    }
}
