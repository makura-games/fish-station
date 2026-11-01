using Content.Shared._Fish.Xenobiology.Slimes;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Random;

namespace Content.Shared._Fish.Xenobiology.Extracts;

public sealed partial class FishSlimeExtractSystem
{
    private static readonly FixedPoint2 One = FixedPoint2.New(1);
    private static readonly FixedPoint2 Ten = FixedPoint2.New(10);

    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private void TryColorReactions(
        Entity<FishSlimeExtractComponent> ent,
        Entity<SolutionComponent> soln,
        Solution solution)
    {
        switch (ent.Comp.Color)
        {
            case "grey":
                TryGreyReaction(ent, soln, solution);
                break;
            case "green":
                TryGreenReaction(ent, soln, solution);
                break;
            case "metal":
                TryMetalReaction(ent, soln, solution);
                break;
            case "blue":
                TryBlueReaction(ent, soln, solution);
                break;
            case "darkblue":
                TryDarkBlueReaction(ent, soln, solution);
                break;
            case "orange":
                TryOrangeReaction(ent, soln, solution);
                break;
            case "yellow":
                TryYellowReaction(ent, soln, solution);
                break;
            case "purple":
                TryPurpleReaction(ent, soln, solution);
                break;
            case "darkpurple":
                TryDarkPurpleReaction(ent, soln, solution);
                break;
            case "red":
                TryRedReaction(ent, soln, solution);
                break;
            case "pink":
                TryPinkReaction(ent, soln, solution);
                break;
            case "black":
                TryBlackReaction(ent, soln, solution);
                break;
            case "oil":
                TryOilReaction(ent, soln, solution);
                break;
            case "lightpink":
                TryLightPinkReaction(ent, soln, solution);
                break;
            case "cerulean":
                TryCeruleanReaction(ent, soln, solution);
                break;
            case "rainbow":
                TryRainbowReaction(ent, soln, solution);
                break;
        }
    }

    private void TryGreenReaction(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        TryReaction(ent, soln, solution, "Plasma", One, () =>
            _solutions.TryAddReagent(soln, "UnstableMutagen", One));
    }

    private void TryMetalReaction(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        if (TryReaction(ent, soln, solution, "Plasma", One, () =>
            {
                SpawnSheets("SheetPlasteel1", 5, ent);
                SpawnSheets("SheetSteel1", 15, ent);
            }))
            return;

        if (TryReaction(ent, soln, solution, "Water", One, () =>
            {
                SpawnSheets("SheetRGlass1", 5, ent);
                SpawnSheets("SheetGlass1", 15, ent);
            }))
            return;

        TryReaction(ent, soln, solution, "Blood", One, () =>
            _solutions.TryAddReagent(soln, "Omnizine", One));
    }

    private void TryBlueReaction(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        if (TryReaction(ent, soln, solution, "Plasma", One, () =>
            _solutions.TryAddReagent(soln, "FrostOil", Ten)))
            return;

        TryReaction(ent, soln, solution, "Blood", One, () =>
            SpawnNextToOrDrop("FishSlimePotionStabilizerFish", ent));
    }

    private void TryDarkBlueReaction(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        TryReaction(ent, soln, solution, "Water", One, () =>
            SpawnNextToOrDrop("FishSlimePotionFireproofFish", ent));
    }

    private void TryOrangeReaction(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        TryReaction(ent, soln, solution, "Blood", One, () =>
            _solutions.TryAddReagent(soln, "CapsaicinOil", Ten));
    }

    private void TryYellowReaction(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        if (TryReaction(ent, soln, solution, "Plasma", One, () =>
            SpawnNextToOrDrop("PowerCellHigh", ent)))
            return;

        TryReaction(ent, soln, solution, "Water", One, () =>
            SpawnNextToOrDrop("FlashlightLantern", ent));
    }

    private void TryPurpleReaction(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        if (TryReaction(ent, soln, solution, "Plasma", One, () =>
            SpawnNextToOrDrop("FishSlimePotionSteroidFish", ent)))
            return;

        TryReaction(ent, soln, solution, "Sugar", One, () =>
            _solutions.TryAddReagent(soln, "Slime", Ten));
    }

    private void TryDarkPurpleReaction(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        TryReaction(ent, soln, solution, "Plasma", One, () =>
            SpawnSheets("SheetPlasma1", 3, ent));
    }

    private void TryRedReaction(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        if (TryReaction(ent, soln, solution, "Plasma", One, () =>
            SpawnNextToOrDrop("FishSlimePotionMutatorFish", ent)))
            return;

        if (TryReaction(ent, soln, solution, "Water", One, () =>
            SpawnNextToOrDrop("FishSlimePotionSpeedFish", ent)))
            return;

        TryReaction(ent, soln, solution, "Blood", One, () => ApplyRedBloodlust(ent));
    }

    private void TryPinkReaction(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        if (TryReaction(ent, soln, solution, "Plasma", One, () =>
            SpawnNextToOrDrop("FishSlimePotionDocilityFish", ent)))
            return;

        TryReaction(ent, soln, solution, "Blood", One, () =>
            _solutions.TryAddReagent(soln, "Omnizine", One));
    }

    private void TryBlackReaction(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        TryReaction(ent, soln, solution, "Plasma", One, () =>
            _solutions.TryAddReagent(soln, "StableMutagen", One));
    }

    private void TryOilReaction(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        TryReaction(ent, soln, solution, "Blood", One, () =>
            SpawnNextToOrDrop("FishSlimePotionOilSlickFish", ent));
    }

    private void TryLightPinkReaction(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        TryReaction(ent, soln, solution, "Plasma", One, () =>
            SpawnNextToOrDrop("FishSlimePotionSentienceFish", ent));
    }

    private void TryCeruleanReaction(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        TryReaction(ent, soln, solution, "Plasma", One, () =>
            SpawnNextToOrDrop("FishSlimePotionEnhancerFish", ent));
    }

    private void TryRainbowReaction(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        if (TryReaction(ent, soln, solution, "Plasma", One, () =>
            {
                var color = _random.Pick(FishSlimeMutation.Colors);
                var adult = _random.Prob(0.5f);
                var coords = _transform.GetMapCoordinates(ent);
                Spawn(FishSlimeMutation.EntityId(color, adult), coords);
            }))
            return;

        TryReaction(ent, soln, solution, "Blood", One, () =>
            SpawnNextToOrDrop("FishSlimePotionTransferenceFish", ent));
    }

    private void ApplyRedBloodlust(Entity<FishSlimeExtractComponent> ent)
    {
        foreach (var uid in _lookup.GetEntitiesInRange(ent.Owner, 7f))
        {
            if (!TryComp<FishSlimeComponent>(uid, out var slime))
                continue;

            if (slime.Docile)
                slime.Docile = false;
            else
                slime.Rabid = true;

            Dirty(uid, slime);
        }
    }

    private void SpawnSheets(string prototype, int count, EntityUid near)
    {
        for (var i = 0; i < count; i++)
            SpawnNextToOrDrop(prototype, near);
    }
}
