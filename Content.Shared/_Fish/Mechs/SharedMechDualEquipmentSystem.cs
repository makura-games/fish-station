using Content.Shared._Fish.Mechs.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Popups;
using Robust.Shared.Containers;

namespace Content.Shared._Fish.Mechs;

/// <summary>
/// Dual-hand: swap primary/secondary selected equipment.
/// </summary>
public abstract class SharedMechDualEquipmentSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechDualEquipmentComponent, MechSwapEquipmentHandsEvent>(OnSwap);
        // Не MechEquipment+Removed — conflict с BattleShuttleSystem.
        SubscribeLocalEvent<MechDualEquipmentComponent, EntRemovedFromContainerMessage>(OnContainerRemoved);
    }

    private void OnSwap(Entity<MechDualEquipmentComponent> ent, ref MechSwapEquipmentHandsEvent args)
    {
        if (args.Handled || !TryComp(ent, out MechComponent? mech))
            return;

        args.Handled = true;

        var primary = mech.CurrentSelectedEquipment;
        mech.CurrentSelectedEquipment = ent.Comp.SecondarySelectedEquipment;
        ent.Comp.SecondarySelectedEquipment = primary;
        Dirty(ent);
        Dirty(ent.Owner, mech);

        var popup = mech.CurrentSelectedEquipment is not null
            ? Loc.GetString("mech-equipment-swap-popup", ("item", mech.CurrentSelectedEquipment.Value))
            : Loc.GetString("mech-equipment-swap-none-popup");
        _popup.PopupClient(popup, ent, args.Performer);
    }

    private void OnContainerRemoved(Entity<MechDualEquipmentComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (!TryComp(ent, out MechComponent? mech))
            return;

        if (args.Container.ID != mech.EquipmentContainerId)
            return;

        if (ent.Comp.SecondarySelectedEquipment == args.Entity)
        {
            ent.Comp.SecondarySelectedEquipment = null;
            Dirty(ent);
        }
    }

    /// <summary>
    /// Назначает оборудование во secondary (API для UI).
    /// </summary>
    public void AssignSecondary(EntityUid mech, EntityUid? equipment, MechDualEquipmentComponent? dual = null, MechComponent? mechComp = null)
    {
        if (!Resolve(mech, ref dual) || !Resolve(mech, ref mechComp))
            return;

        if (equipment != null &&
            (!HasComp<MechEquipmentComponent>(equipment.Value) || !mechComp.EquipmentContainer.Contains(equipment.Value)))
            return;

        dual.SecondarySelectedEquipment = equipment;
        Dirty(mech, dual);
    }
}
