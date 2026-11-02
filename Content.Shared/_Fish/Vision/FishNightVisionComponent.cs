using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Vision;

/// <summary>Device optics granted to the wearer by the existing clothing toggle.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishNightVisionComponent : Component
{
    /// <summary>Phosphor tint, independent of illumination and gain.</summary>
    [DataField, AutoNetworkedField]
    public Color Color = new(0.06f, 1f, 0.12f);

    /// <summary>Neutral lighting lift before the world is shaded.</summary>
    [DataField, AutoNetworkedField]
    public Color LightingColor = new(1f, 1f, 1f, 0.15f);
}
