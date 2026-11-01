using Content.Shared._Fish.Xenobiology.Extracts;
using Content.Shared._Fish.Xenobiology.Slimes;

namespace Content.Server._Fish.Xenobiology.Extracts;

public sealed partial class FishSlimePotionSystem
{
    /// <summary>
    /// Кормление слайма зельем с консоли (без предмета зелья).
    /// </summary>
    public bool TryApplyFromConsole(EntityUid slimeUid, FishSlimePotionKind kind, EntityUid user)
    {
        if (!TryComp<FishSlimeComponent>(slimeUid, out var slime))
            return false;

        return TryApply((slimeUid, slime), kind, user, EntityUid.Invalid);
    }
}
