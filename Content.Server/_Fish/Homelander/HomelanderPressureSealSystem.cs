using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Server._Fish.Homelander;

[RegisterComponent]
public sealed partial class HomelanderPressureSealComponent : Component;

public sealed partial class HomelanderPressureSealSystem : EntitySystem
{
    private const string OuterClothing = "outerClothing";

    [Dependency] private BarotraumaSystem _barotrauma = default!;
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HomelanderPressureSealComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HomelanderPressureSealComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<BarotraumaComponent, RefreshPressureImmunityEvent>(OnRefreshImmunity);
    }

    private void OnEquipped(EntityUid uid, HomelanderPressureSealComponent component, GotEquippedEvent args)
    {
        if (args.Slot == OuterClothing)
            _barotrauma.RefreshPressureImmunity(args.EquipTarget);
    }

    private void OnUnequipped(EntityUid uid, HomelanderPressureSealComponent component, GotUnequippedEvent args)
    {
        if (args.Slot == OuterClothing)
            _barotrauma.RefreshPressureImmunity(args.EquipTarget);
    }

    private void OnRefreshImmunity(Entity<BarotraumaComponent> entity, ref RefreshPressureImmunityEvent args)
    {
        if (_inventory.TryGetSlotEntity(entity, OuterClothing, out var suit) &&
            HasComp<HomelanderPressureSealComponent>(suit))
            args.IsImmune = true;
    }
}
