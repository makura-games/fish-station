using Robust.Shared.GameStates;

namespace Content.Shared._Fish.BattleShuttles.Components;

/// <summary>
/// Замок шаттла. LockId = 0 назначается при MapInit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BattleShuttleLockComponent : Component
{
    [DataField, AutoNetworkedField]
    public int LockId;
}
