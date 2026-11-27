using System.Linq;
using Content.Client.Items.Systems;
using Content.Shared.Hands;
using Content.Shared.Item;
using Content.Shared.Toggleable;
using Robust.Client.GameObjects;

namespace Content.Client._Fish.ItemSwitch;

/// <summary>
/// Система визуализации дополнительных слоёв в руках при переключении состояний <see cref="ItemSwitchVisualsComponent"/>.
/// </summary>
public sealed partial class ItemSwitchVisualsSystem : VisualizerSystem<ItemSwitchVisualsComponent>
{
    [Dependency] private SharedItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemSwitchVisualsComponent, GetInhandVisualsEvent>(OnGetHeldVisuals,
            after: [typeof(ItemSystem)]);
    }

    protected override void OnAppearanceChange(EntityUid uid,
        ItemSwitchVisualsComponent component,
        ref AppearanceChangeEvent args)
    {
        _item.VisualsChanged(uid);
    }

    private void OnGetHeldVisuals(EntityUid uid, ItemSwitchVisualsComponent component, GetInhandVisualsEvent args)
    {
        if (!TryComp(uid, out AppearanceComponent? appearance)
            || !AppearanceSystem.TryGetData<string>(uid, SwitchableVisuals.Switched, out var state, appearance))
            return;

        if (!component.InhandVisuals.TryGetValue(state, out var handVisuals)
            || !handVisuals.TryGetValue(args.Location, out var layers))
            return;

        var i = 0;
        var defaultKey = $"inhand-{args.Location.ToString().ToLowerInvariant()}-switch";
        foreach (var layer in layers)
        {
            var key = layer.MapKeys?.FirstOrDefault();
            if (key == null)
            {
                key = i == 0 ? defaultKey : $"{defaultKey}-{i}";
                i++;
            }

            args.Layers.Add((key, layer));
        }
    }
}
