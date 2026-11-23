using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared._Fish.Effects;

namespace Content.Server._Fish.Effects;

[Serializable, DataDefinition]
public sealed partial class FishDebrisBehavior : IThresholdBehavior
{
    [DataField(required: true)]
    public FishParticleKind Kind;

    public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
        => system.EntityManager.System<FishActionParticleSystem>().Emit(owner, Kind);
}
