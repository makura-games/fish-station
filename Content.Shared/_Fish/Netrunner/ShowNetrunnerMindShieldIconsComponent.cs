using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Netrunner;

/// <summary>Enables the unique netrunner visor HUD displaying genuine mindshield presence or absence on mobs.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShowNetrunnerMindShieldIconsComponent : Component;
