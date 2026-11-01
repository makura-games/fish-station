using Content.Server.StationEvents.Components;
using Content.Shared._Fish.Xenobiology.Slimes;

namespace Content.Server._Fish.Xenobiology.Extracts;

public sealed partial class FishSlimePotionSystem
{
    private bool ApplySentienceOnSlime(Entity<FishSlimeComponent> slime, EntityUid user, EntityUid potion)
    {
        var sentience = EnsureComp<SentienceTargetComponent>(slime);
        return TrySentience((slime, sentience), user, potion);
    }
}
