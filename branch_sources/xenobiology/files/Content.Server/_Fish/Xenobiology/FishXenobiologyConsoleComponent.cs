namespace Content.Server._Fish.Xenobiology;

/// <summary>Displays the local colony's measurements and the breed reference catalog.</summary>
[RegisterComponent]
public sealed partial class FishXenobiologyConsoleComponent : Component
{
    /// <summary>Maximum measurement distance in world metres.</summary>
    [DataField]
    public float Range = 20f;
}
