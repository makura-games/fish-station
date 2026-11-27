using Robust.Shared.Serialization;

namespace Content.Shared.Anomaly.Effects.Components;

public sealed partial class GravityAnomalyComponent
{
    /// <summary>Действующие границы и ускорения гравитационного поля для движения пыли.</summary>
    [AutoNetworkedField]
    public GravityAnomalyField Field;
}

[Serializable, NetSerializable]
public readonly record struct GravityAnomalyField(float InnerRadius, float OuterRadius, float Radial, float Tangential);
