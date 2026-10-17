using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Mech.Components;

/// <summary>
/// Controls whether BorgBrain entities can pilot this mech.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechBrainComponent : Component
{
    /// <summary>
    /// Allows brain installation, bypassing the normal movement and pilot whitelist requirements.
    /// Pilot blacklists and the installer's access requirements still apply.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanInsertBrain;
}
