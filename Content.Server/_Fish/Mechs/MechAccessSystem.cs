using Content.Server.Mech.Systems;
using Content.Shared._Fish.Mechs;
using Content.Shared._Fish.Mechs.Components;
using Content.Shared.Interaction;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mech.Equipment.Components;

namespace Content.Server._Fish.Mechs;

/// <summary>
/// DNA-gate и maintenance install-gate перед MechSystem / MechEquipmentSystem.
/// </summary>
public sealed class MechAccessSystem : SharedMechAccessSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechDnaLockComponent, MechEntryEvent>(OnDnaEntry, before: [typeof(MechSystem)]);
        // Directed на InstallGate, не на MechEquipment — иначе duplicate с MechEquipmentSystem.
        SubscribeLocalEvent<MechEquipmentInstallGateComponent, AfterInteractEvent>(OnEquipmentInstallAttempt, before: [typeof(MechEquipmentSystem)]);
    }

    private void OnDnaEntry(Entity<MechDnaLockComponent> ent, ref MechEntryEvent args)
    {
        HandleDnaEntry(ent, ref args);
    }

    private void OnEquipmentInstallAttempt(Entity<MechEquipmentInstallGateComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        if (!TryComp(args.Target.Value, out MechMaintenanceComponent? maint))
            return;

        if (maint.State == MechMaintenanceState.Ready)
            return;

        args.Handled = true;
        Popup.PopupClient(Loc.GetString("mech-maint-blocks-equipment"), args.Target.Value, args.User);
    }
}
