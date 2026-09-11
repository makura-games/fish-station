using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Artillery;

[Serializable, NetSerializable]
public sealed partial class BluespaceArtillerySetParamsMessage : BoundUserInterfaceMessage
{
    public string ExplosionType { get; set; } = "Default";
    public float TotalIntensity { get; set; } = 100f;
    public float Slope { get; set; } = 5f;
    public float MaxIntensity { get; set; } = 50f;
}