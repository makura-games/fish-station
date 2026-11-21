namespace Content.Shared._Fish.Netrunner;

/// <summary>Wearable cooldown accelerator with increased neural activation heat.</summary>
[RegisterComponent]
public sealed partial class NetrunnerVisorComponent : Component
{
    /// <summary>Divides normal cooldowns by 1.5 while worn in the eyes slot.</summary>
    [DataField] public float CooldownMultiplier = 1f / 1.5f;
    /// <summary>Multiplier applied to body heat generated when a program starts.</summary>
    [DataField] public float HeatMultiplier = 2;
}
