using Content.Shared.Mech.Components;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared._Fish.Mech.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._Fish.Mech;

/// <summary>
/// Opens a mech's mass scanner for its pilot and revokes it when piloting ends.
/// </summary>
public sealed partial class MechMassScannerSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechComponent, MechOpenMassScannerEvent>(OnOpenMassScanner);
        SubscribeLocalEvent<MechPilotComponent, ComponentRemove>(OnPilotRemoved);
    }

    private void OnOpenMassScanner(Entity<MechComponent> ent, ref MechOpenMassScannerEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryOpenMassScanner(ent, args.Performer);
    }

    private void OnPilotRemoved(Entity<MechPilotComponent> ent, ref ComponentRemove args)
    {
        _ui.CloseUi(ent.Comp.Mech, RadarConsoleUiKey.Key, ent.Owner);
    }

    /// <summary>
    /// Toggles the mass scanner for the current pilot of a functioning mech.
    /// </summary>
    public bool TryOpenMassScanner(Entity<MechComponent> ent, EntityUid performer)
    {
        if (!CanOpenMassScanner(ent, performer))
            return false;

        return DoOpenMassScanner(ent, performer);
    }

    /// <summary>
    /// Checks occupancy, player control and the presence of the scanner interface.
    /// </summary>
    public bool CanOpenMassScanner(Entity<MechComponent> ent, EntityUid performer)
    {
        if (ent.Comp.Broken || ent.Comp.PilotSlot.ContainedEntity != performer)
            return false;

        if (!HasComp<ActorComponent>(performer))
            return false;

        return _ui.HasUi(ent, RadarConsoleUiKey.Key);
    }

    private bool DoOpenMassScanner(EntityUid mech, EntityUid performer)
    {
        var wasOpen = _ui.IsUiOpen(mech, RadarConsoleUiKey.Key, performer);
        return _ui.TryToggleUi(mech, RadarConsoleUiKey.Key, performer) &&
               _ui.IsUiOpen(mech, RadarConsoleUiKey.Key, performer) != wasOpen;
    }
}
