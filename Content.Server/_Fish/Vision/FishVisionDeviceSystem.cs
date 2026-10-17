using Content.Shared._Fish.Vision;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Server._Fish.Vision;

/// <summary>Resolves active goggles after generic toggles without changing other toggleable components.</summary>
public sealed partial class FishVisionDeviceSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishVisionDeviceComponent, ItemToggledEvent>(OnToggled, after: [typeof(ComponentTogglerSystem)]);
        SubscribeLocalEvent<FishVisionDeviceComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnToggled(Entity<FishVisionDeviceComponent> ent, ref ItemToggledEvent args)
    {
        if (TryComp<ComponentTogglerComponent>(ent, out var toggler))
            RefreshWearer((ent.Owner, toggler));
    }

    private void OnShutdown(Entity<FishVisionDeviceComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<ComponentTogglerComponent>(ent, out var toggler))
            RefreshWearer((ent.Owner, toggler), ent.Owner);
    }

    private void RefreshWearer(Entity<ComponentTogglerComponent> device, EntityUid? excluded = null)
    {
        if (!device.Comp.Parent || device.Comp.Target is not { } wearer || TerminatingOrDeleted(wearer))
            return;

        var configuredComponents = device.Comp.Components;
        if (configuredComponents.TryGetComponent<FishNightVisionComponent>(_componentFactory, out _))
        {
            // При одновременном ПНВ на голове и глазах очки имеют устойчивый приоритет.
            if (TryGetSource<FishNightVisionComponent>(wearer, "eyes", excluded, out var source) ||
                TryGetSource(wearer, "head", excluded, out source))
            {
                var vision = EnsureComp<FishNightVisionComponent>(wearer);
                vision.Color = source!.Color;
                vision.LightingColor = source.LightingColor;
                Dirty(wearer, vision);
            }
            else
                RemComp<FishNightVisionComponent>(wearer);
        }

        if (configuredComponents.TryGetComponent<FishThermalVisionComponent>(_componentFactory, out _))
        {
            if (TryGetSource<FishThermalVisionComponent>(wearer, "eyes", excluded, out var source))
            {
                var vision = EnsureComp<FishThermalVisionComponent>(wearer);
                vision.Range = source!.Range;
                Dirty(wearer, vision);
            }
            else
                RemComp<FishThermalVisionComponent>(wearer);
        }
    }

    private bool TryGetSource<T>(EntityUid wearer, string slot, EntityUid? excluded, out T? source)
        where T : Component, new()
    {
        source = null;
        if (!_inventory.TryGetSlotEntity(wearer, slot, out var item) || item == excluded ||
            TerminatingOrDeleted(item) || !_itemToggle.IsActivated(item.Value) ||
            !TryComp<ComponentTogglerComponent>(item, out var toggler))
            return false;

        var configuredComponents = toggler.Components;
        return configuredComponents.TryGetComponent(_componentFactory, out source);
    }
}
