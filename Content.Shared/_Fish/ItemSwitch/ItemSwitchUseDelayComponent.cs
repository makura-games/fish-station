using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ItemSwitch;

/// <summary>
/// Запрещает переключение состояния <see cref="Content.Shared.Item.ItemToggle.Components.ItemSwitchComponent"/>
/// во время активной задержки <see cref="Content.Shared.Timing.Components.UseDelayComponent"/> и сбрасывает её после переключения.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ItemSwitchUseDelayComponent : Component
{
}
