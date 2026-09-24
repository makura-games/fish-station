using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Lightning;

[Serializable, NetSerializable]
public enum SkyLightningVisuals : byte
{
    Tilt,
    Variant,
}

[Serializable, NetSerializable]
public enum SkyLightningLayers : byte
{
    Bolt,
}
