namespace Content.Shared._Fish.Xenobiology.Machines;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishMonkeyRecyclerComponent : Component
{
    [DataField]
    public int RequiredGrind = 5;

    [DataField]
    public int Stored;

    [DataField, AutoNetworkedField]
    public EntityUid? LinkedConsole;
}
