using Content.Shared.Hands.Components;

namespace Content.Client._Fish.ItemSwitch;

/// <summary>
/// Компонент визуализации дополнительных слоёв в руках для состояний <see cref="Content.Shared.Item.ItemToggle.Components.ItemSwitchComponent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class ItemSwitchVisualsComponent : Component
{
    /// <summary>
    /// Слои, добавляемые к спрайту игрока, держащего предмет, в зависимости от активного состояния переключателя.
    /// </summary>
    [DataField]
    public Dictionary<string, Dictionary<HandLocation, List<PrototypeLayerData>>> InhandVisuals = new();
}
