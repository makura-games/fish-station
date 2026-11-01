namespace Content.Shared._Fish.Xenobiology.Organs;

public enum FishXenoOrganEffectKind : byte
{
    Regenerative,
    Toxic,
    Sinew,
    Vigor,
    Antitoxin,
    HeatResist,
    EmpPulse,
    Adrenaline,
    SlimeSkin,
}

[RegisterComponent]
public sealed partial class FishXenoOrganEffectComponent : Component
{
    [DataField(required: true)]
    public FishXenoOrganEffectKind Kind;
}

[RegisterComponent]
public sealed partial class FishXenoOrganBuffComponent : Component
{
    public FishXenoOrganEffectKind Kind;
    public TimeSpan EndsAt;
}
