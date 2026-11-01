namespace Content.Shared._Fish.Xenobiology.Extracts;

/// <summary>
/// «Разогрев» зелья sentience: два холостых применения, третье взрывается (упрощённый SS220).
/// </summary>
[RegisterComponent]
public sealed partial class FishSlimeSentiencePotionComponent : Component
{
    [DataField]
    public int WarmStage;
}
