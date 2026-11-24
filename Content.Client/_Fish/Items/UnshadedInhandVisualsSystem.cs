using Content.Client.Items.Systems;
using Content.Shared.Hands;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client._Fish.Items;

/// <summary>
/// Система, добавляющая к предмету в руках постоянный <c>unshaded</c>-слой по суффиксу стейта.
/// </summary>
public sealed partial class UnshadedInhandVisualsSystem : EntitySystem
{
    [Dependency] private IResourceCache _resCache = default!;
    [Dependency] private EntityQuery<ItemComponent> _itemQuery = default!;
    [Dependency] private EntityQuery<SpriteComponent> _spriteQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UnshadedInhandVisualsComponent, GetInhandVisualsEvent>(OnGetInhandVisuals,
            after: [typeof(ItemSystem)]);
    }

    private void OnGetInhandVisuals(Entity<UnshadedInhandVisualsComponent> ent, ref GetInhandVisualsEvent args)
    {
        if (!_itemQuery.TryComp(ent, out var item))
            return;

        RSI? rsi = null;
        if (item.RsiPath != null)
            rsi = _resCache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / item.RsiPath).RSI;
        else if (_spriteQuery.TryComp(ent, out var sprite))
            rsi = sprite.BaseRSI;

        if (rsi == null)
            return;

        var defaultKey = $"inhand-{args.Location.ToString().ToLowerInvariant()}";
        var baseState = item.HeldPrefix == null
            ? defaultKey
            : $"{item.HeldPrefix}-{defaultKey}";
        var lightState = $"{baseState}{ent.Comp.Suffix}";

        if (!rsi.TryGetState(lightState, out _))
            return;

        args.Layers.Add((lightState, new PrototypeLayerData
        {
            RsiPath = rsi.Path.ToString(),
            State = lightState,
            Shader = "unshaded",
            MapKeys = [lightState]
        }));
    }
}
