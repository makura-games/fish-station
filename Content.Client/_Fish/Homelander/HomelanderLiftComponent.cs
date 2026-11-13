using System.Numerics;

namespace Content.Client._Fish.Homelander;

[RegisterComponent]
public sealed partial class HomelanderLiftComponent : Component
{
    public Vector2 GroundOffset;
    public Angle GroundRotation;
    public Angle ShadowRenderRotation;
    public float Lean;
    public float Progress;
    public bool Flying;
}
