using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Маркер для AfterInteract-gate установки (directed uniqueness vs MechEquipmentSystem).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MechEquipmentInstallGateComponent : Component;
