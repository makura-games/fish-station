using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Vision;

/// <summary>Configures natural low-light rendering for an innate NightVision ability.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FishNaturalNightVisionComponent : Component
{
    /// <summary>Neutral illumination supplied by fully adapted eyes, from zero to 0.35.</summary>
    [DataField, AutoNetworkedField]
    public float LightingStrength = 0.25f;

    /// <summary>Radius in world units that receives the full natural light boost.</summary>
    [DataField, AutoNetworkedField]
    public float FullLightRadius = 2.5f;

    /// <summary>Distance over which the natural light boost smoothly fades to zero.</summary>
    [DataField, AutoNetworkedField]
    public float LightFadeWidth = 0.5f;

    /// <summary>Seconds needed to complete 95% of adaptation to darkness.</summary>
    [DataField, AutoNetworkedField]
    public float DarkAdaptationSeconds = 1.75f;

    /// <summary>Seconds needed to complete 95% of adaptation to bright light.</summary>
    [DataField, AutoNetworkedField]
    public float LightAdaptationSeconds = 0.2f;

    /// <summary>Color saturation retained in the darkest parts of the view.</summary>
    [DataField, AutoNetworkedField]
    public float DarknessSaturation = 0.15f;

    /// <summary>Soft contrast enhancement that preserves black and white.</summary>
    [DataField, AutoNetworkedField]
    public float ShadowContrast = 1.12f;

    /// <summary>Multiplicative low-light grain strength; zero disables the grain.</summary>
    [DataField, AutoNetworkedField]
    public float GrainStrength = 0.4f;

    /// <summary>Low-light tint; alpha controls its strength.</summary>
    [DataField, AutoNetworkedField]
    public Color Tint = Color.White;
}
