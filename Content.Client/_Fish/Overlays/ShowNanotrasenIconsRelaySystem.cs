using Content.Shared._Fish.Overlays;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Client._Fish.Overlays;

public sealed partial class ShowNanotrasenIconsRelaySystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventoryComponent, RefreshEquipmentHudEvent<ShowNanotrasenIconsComponent>>(OnRefresh);
    }

    private void OnRefresh(Entity<InventoryComponent> ent, ref RefreshEquipmentHudEvent<ShowNanotrasenIconsComponent> args)
    {
        _inventory.RelayEvent(ent, ref args);
    }
}
