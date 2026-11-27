using Content.Shared._Fish.Effects;
using Content.Shared.Body.Components;
using Content.Shared.FixedPoint;

namespace Content.Shared.Body.Systems;

public sealed partial class BloodstreamSystem
{
    /// <summary>Выпускает кровь из кровеносной системы обычными каплями.</summary>
    public bool TryBleedOut(Entity<BloodstreamComponent?> ent, FixedPoint2 amount)
        => TryBleedOut(ent, amount, FishParticleKind.BloodDrip);
}
