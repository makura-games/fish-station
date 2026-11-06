using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ObrNadzor;

/// <summary>
/// Временный маркер оператора в режиме наблюдения консоли надзора.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ObrNadzorObserverComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Console;

    [DataField, AutoNetworkedField]
    public EntityUid? StopObserveAction;
}
