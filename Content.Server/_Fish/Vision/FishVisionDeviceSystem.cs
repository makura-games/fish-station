using Content.Shared._Fish.Vision;
using Content.Shared._Sunrise.ThermalVision;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Server._Fish.Vision;

/// <summary>Выбирает активную оптику экипировки и термальных имплантов.</summary>
public sealed partial class FishVisionDeviceSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;

    private const float IntrinsicThermalRange = 16f;

    // Очки приоритетнее шлема, термалы дополнительно могут стоять в маске.
    private static readonly string[] NightVisionSlots = ["eyes", "head"];
    private static readonly string[] ThermalVisionSlots = ["eyes", "head", "mask"];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishVisionDeviceComponent, ItemToggledEvent>(OnToggled, after: [typeof(ComponentTogglerSystem)]);
        SubscribeLocalEvent<FishVisionDeviceComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ThermalVisionComponent, ComponentStartup>(OnIntrinsicThermalStartup);
        SubscribeLocalEvent<ThermalVisionComponent, ComponentShutdown>(OnIntrinsicThermalShutdown);
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

    private void OnIntrinsicThermalStartup(Entity<ThermalVisionComponent> ent, ref ComponentStartup args) =>
        RefreshThermalVision(ent);

    private void OnIntrinsicThermalShutdown(Entity<ThermalVisionComponent> ent, ref ComponentShutdown args) =>
        RefreshThermalVision(ent, includeIntrinsic: false);

    private void RefreshWearer(Entity<ComponentTogglerComponent> device, EntityUid? excluded = null)
    {
        if (!device.Comp.Parent || device.Comp.Target is not { } wearer || TerminatingOrDeleted(wearer))
            return;

        var configuredComponents = device.Comp.Components;
        if (configuredComponents.TryGetComponent<FishNightVisionComponent>(_componentFactory, out _))
        {
            // При одновременном ПНВ на голове и глазах очки имеют устойчивый приоритет.
            if (TryGetSource<FishNightVisionComponent>(wearer, NightVisionSlots, excluded, out var source))
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
            RefreshThermalVision(wearer, excluded);
    }

    private void RefreshThermalVision(EntityUid wearer, EntityUid? excluded = null, bool includeIntrinsic = true)
    {
        if (TerminatingOrDeleted(wearer))
            return;

        var range = IntrinsicThermalRange;
        if (TryGetSource<FishThermalVisionComponent>(wearer, ThermalVisionSlots, excluded, out var source))
            range = source!.Range;
        else if (!includeIntrinsic || !HasComp<ThermalVisionComponent>(wearer))
        {
            RemComp<FishThermalVisionComponent>(wearer);
            return;
        }

        var vision = EnsureComp<FishThermalVisionComponent>(wearer);
        vision.Range = range;
        Dirty(wearer, vision);
    }

    private bool TryGetSource<T>(EntityUid wearer, string[] slots, EntityUid? excluded, out T? source)
        where T : Component, new()
    {
        source = null;
        foreach (var slot in slots)
        {
            if (!_inventory.TryGetSlotEntity(wearer, slot, out var item) || item == excluded ||
                TerminatingOrDeleted(item) || !_itemToggle.IsActivated(item.Value) ||
                !TryComp<ComponentTogglerComponent>(item, out var toggler))
                continue;

            var configuredComponents = toggler.Components;
            if (configuredComponents.TryGetComponent(_componentFactory, out source))
                return true;
        }

        source = null;
        return false;
    }
}
