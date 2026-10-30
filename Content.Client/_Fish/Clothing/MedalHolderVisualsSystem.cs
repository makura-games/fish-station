using System.Numerics;
using Content.Shared._Fish.Clothing;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;

namespace Content.Client._Fish.Clothing;

/// <summary>
/// Добавляет слои медали из ItemSlot формы к GetEquipmentVisualsEvent носителя.
/// </summary>
public sealed partial class MedalHolderVisualsSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private IResourceCache _cache = default!;

    public override void Initialize()
    {
        base.Initialize();

        // После базовых слоёв формы, чтобы медаль была поверх.
        SubscribeLocalEvent<MedalHolderComponent, GetEquipmentVisualsEvent>(OnGetVisuals, after: [typeof(ClothingSystem)]);
    }

    private void OnGetVisuals(Entity<MedalHolderComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (!TryComp<ItemSlotsComponent>(ent, out var slots))
            return;

        if (!_itemSlots.TryGetSlot((ent, slots), ent.Comp.SlotId, out var slot) || slot.Item is not { } medal)
            return;

        if (!TryGetMedalLayer(medal, out var layerData))
            return;

        var key = $"{args.Slot}-medal";
        args.Layers.Add((key, layerData));
    }

    /// <summary>
    /// Берёт спрайт медали из RSI state equipped-NECK (стандарт медалей/значков).
    /// </summary>
    private bool TryGetMedalLayer(EntityUid medal, out PrototypeLayerData layer)
    {
        layer = new PrototypeLayerData();

        RSI? rsi = null;
        Vector2 scale = Vector2.One;

        if (TryComp<ClothingComponent>(medal, out var clothing))
        {
            scale = clothing.Scale;
            if (clothing.RsiPath != null)
                rsi = _cache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / clothing.RsiPath).RSI;
        }

        if (rsi == null && TryComp<SpriteComponent>(medal, out var medalSprite))
            rsi = medalSprite.BaseRSI;

        if (rsi == null || !rsi.TryGetState("equipped-NECK", out _))
            return false;

        layer = new PrototypeLayerData
        {
            RsiPath = rsi.Path.ToString(),
            State = "equipped-NECK",
            Scale = scale
        };
        return true;
    }
}
