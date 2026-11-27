using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared._Fish.Effects;
using Content.Shared.Destructible;
using Content.Shared.Destructible.Thresholds.Triggers;

namespace Content.Server._Fish.Effects;

[Serializable, DataDefinition]
public sealed partial class FishDebrisBehavior : IThresholdBehavior
{
    [DataField(required: true)]
    public FishParticleKind Kind;

    internal static void EmitOnOverkill(Entity<DestructibleComponent> entity, DamageThreshold threshold, DestructibleSystem system)
    {
        if (threshold.Behaviors is not [DoActsBehavior { Acts: ThresholdActs.Destruction }] ||
            threshold.Trigger is not DamageTrigger overkill)
            return;

        // При мгновенном удалении обычный порог разрушения уже не выполнится.
        foreach (var normal in entity.Comp.Thresholds)
        {
            if (normal.Triggered || normal.Trigger is not DamageTrigger trigger || trigger.Damage >= overkill.Damage)
                continue;

            if (normal.Behaviors.Find(b => b is FishDebrisBehavior) is not FishDebrisBehavior debris ||
                !normal.Behaviors.Exists(b => b is DoActsBehavior acts && acts.HasAct(ThresholdActs.Destruction)))
                continue;

            debris.Execute(entity, system);
            return;
        }
    }

    public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
        => system.EntityManager.System<FishActionParticleSystem>().Emit(owner, Kind);
}
