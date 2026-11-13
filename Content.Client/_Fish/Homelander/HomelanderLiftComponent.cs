using System.Numerics;

namespace Content.Client._Fish.Homelander;

[RegisterComponent]
public sealed partial class HomelanderLiftComponent : Component
{
    public Vector2 GroundOffset;
    public float Progress;
    public bool Flying;
}
