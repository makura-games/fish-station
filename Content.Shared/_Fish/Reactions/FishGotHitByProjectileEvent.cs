using Content.Shared.Damage;

namespace Content.Shared._Fish.Reactions;

/// <summary>
/// Поднимается на цели после попадания снаряда (Fish-edit в ProjectileSystem).
/// Origin урона у пули = shooter, поэтому цели нужен отдельный сигнал.
/// </summary>
[ByRefEvent]
public readonly record struct FishGotHitByProjectileEvent(
    EntityUid Projectile,
    EntityUid? Shooter,
    DamageSpecifier Damage);
