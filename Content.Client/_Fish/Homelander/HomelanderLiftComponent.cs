using System.Numerics;
using Content.Shared.Speech;

namespace Content.Client._Fish.Homelander;

[RegisterComponent]
public sealed partial class HomelanderLiftComponent : Component
{
    public Vector2 GroundOffset;
    public Vector2 WorldTextOffset;
    public SpeechComponent? Speech;
    public float BaseSpeechOffset;
    public float HoverPhase;
    public float Progress;
    public bool Flying;
}
