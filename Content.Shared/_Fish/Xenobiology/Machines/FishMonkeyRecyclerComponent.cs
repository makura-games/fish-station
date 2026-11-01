namespace Content.Shared._Fish.Xenobiology.Machines;

[RegisterComponent]
public sealed partial class FishMonkeyRecyclerComponent : Component
{
    [DataField]
    public int RequiredGrind = 5;

    [DataField]
    public int Stored;
}
