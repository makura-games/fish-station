using Content.Server.Explosion.EntitySystems;
using Content.Shared._Fish.Xenobiology.Extracts;
using Content.Shared.Mobs;

namespace Content.Server._Fish.Xenobiology.Extracts;

/// <summary>
/// Снятие gold crit с капа при смерти моба от экстракта.
/// </summary>
public sealed partial class FishSlimeExtractGoldCritSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishSlimeExtractGoldCritComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<FishSlimeExtractGoldCritComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        FishSlimeExtractGoldMobCap.Release(ent.Comp.CapWeight);
        RemComp<FishSlimeExtractGoldCritComponent>(ent);
    }
}
