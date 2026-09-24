using Content.Shared.Atmos.Components;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._Fish.Melee;

/// <summary>
/// Prevents throw-on-hit knockback for weapons marked with <see cref="ThrowOnHitMagbootsImmuneComponent"/>
/// when the target has active magnetic boots.
/// </summary>
public sealed class MeleeMagbootsImmunitySystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ThrowOnHitMagbootsImmuneComponent, AttemptMeleeThrowOnHitEvent>(OnAttemptMeleeThrowOnHit);
    }

    private void OnAttemptMeleeThrowOnHit(Entity<ThrowOnHitMagbootsImmuneComponent> ent, ref AttemptMeleeThrowOnHitEvent args)
    {
        if (TryComp<MovedByPressureComponent>(args.Target, out var moved) && !moved.Enabled)
            args.Cancelled = true;
    }
}
