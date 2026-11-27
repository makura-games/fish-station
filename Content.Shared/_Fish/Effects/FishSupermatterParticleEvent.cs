using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Effects;

[Serializable, NetSerializable]
public enum FishSupermatterParticleEffect : byte
{
    Chip,
    Charge,
}

[Serializable, NetSerializable]
public sealed class FishSupermatterParticleEvent(NetEntity crystal, FishSupermatterParticleEffect effect) : EntityEventArgs
{
    public const float ChargeSeconds = 0.25f;
    public readonly NetEntity Crystal = crystal;
    public readonly FishSupermatterParticleEffect Effect = effect;
}
