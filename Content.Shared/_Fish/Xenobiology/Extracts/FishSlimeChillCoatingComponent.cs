using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Xenobiology.Extracts;

/// <summary>
/// Нанесённое зелье chill: заряды огнезащиты на одежде (упрощённый паритет SS220).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishSlimeChillCoatingComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Charges = 3;
}
