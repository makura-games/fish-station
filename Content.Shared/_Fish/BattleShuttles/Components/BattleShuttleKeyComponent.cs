using Robust.Shared.GameStates;

namespace Content.Shared._Fish.BattleShuttles.Components;

/// <summary>
/// Ключ от замка шаттла. Null LockId — чистый ключ для импринта.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BattleShuttleKeyComponent : Component
{
    [DataField, AutoNetworkedField]
    public int? LockId;
}
