using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Ipc;

/// <summary>
/// Боевой КПБ (ОБР / NukeOps): батарею шасси нельзя вытащить или заменить.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcSealedBatteryComponent : Component;
