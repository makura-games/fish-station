namespace Content.Shared._Fish.Xenobiology.Organs;

/// <summary>
/// Ксено-орган как подкожный имплант (упрощённый SS220, порция 3).
/// </summary>
[RegisterComponent]
public sealed partial class FishXenoOrganImplantComponent : Component
{
    [DataField(required: true)]
    public FishXenoOrganEffectKind Kind;

    /// <summary>
    /// Длительность эффекта после имплантации (UseInHand по-прежнему 60 с).
    /// </summary>
    [DataField]
    public TimeSpan ImplantDuration = TimeSpan.FromMinutes(30);
}
