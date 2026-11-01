using Content.Shared._Fish.Xenobiology.Slimes;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Fish.Xenobiology.Extracts;

public sealed partial class FishSlimeExtractSystem
{
    private static readonly FixedPoint2 GreyPlasmaCost = FixedPoint2.New(1);
    private static readonly FixedPoint2 GreyWaterCost = FixedPoint2.New(5);
    private static readonly FixedPoint2 GreyBloodCost = FixedPoint2.New(1);
    private static readonly FixedPoint2 GreyEpinephrineYield = FixedPoint2.New(3);
    private const int GreyMonkeyCubeCount = 3;

    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;

    partial void InitializeReactions()
    {
        SubscribeLocalEvent<FishSlimeExtractComponent, SolutionChangedEvent>(OnSolutionChanged);
    }

    private void OnSolutionChanged(Entity<FishSlimeExtractComponent> ent, ref SolutionChangedEvent args)
    {
        if (_net.IsClient || _timing.ApplyingState || ent.Comp.Uses <= 0)
            return;

        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.SolutionId, out var soln, out var solution))
            return;

        TryColorReactions(ent, soln, solution);
    }

    private void TryGreyReaction(
        Entity<FishSlimeExtractComponent> ent,
        Entity<SolutionComponent> soln,
        Solution solution)
    {
        if (TryReaction(
                ent,
                soln,
                solution,
                "Plasma",
                GreyPlasmaCost,
                () =>
                {
                    var coords = _transform.GetMapCoordinates(ent);
                    Spawn(FishSlimeMutation.EntityId("grey", adult: false), coords);
                }))
            return;

        if (TryReaction(
                ent,
                soln,
                solution,
                "Water",
                GreyWaterCost,
                () => _solutions.TryAddReagent(soln, "Epinephrine", GreyEpinephrineYield)))
            return;

        TryReaction(
            ent,
            soln,
            solution,
            "Blood",
            GreyBloodCost,
            () =>
            {
                for (var i = 0; i < GreyMonkeyCubeCount; i++)
                    SpawnNextToOrDrop("FishXenobiologyMonkeyCube", ent);
            });
    }

    private bool TryReaction(
        Entity<FishSlimeExtractComponent> ent,
        Entity<SolutionComponent> soln,
        Solution solution,
        string reagent,
        FixedPoint2 cost,
        Action apply)
    {
        if (solution.GetTotalPrototypeQuantity(reagent) < cost)
            return false;

        _solutions.RemoveReagent(soln, reagent, cost);
        apply();
        FinishReaction(ent);
        return true;
    }

    private void FinishReaction(Entity<FishSlimeExtractComponent> ent)
    {
        PopupReaction(ent, "fish-slime-extract-react");
        ConsumeUse(ent);

        if (ent.Comp.Uses <= 0)
            PopupReaction(ent, "fish-slime-extract-spent");
    }

    private void PopupReaction(Entity<FishSlimeExtractComponent> ent, string locId)
    {
        if (ent.Comp.LastInjector is { } user && Exists(user))
            _popup.PopupEntity(Loc.GetString(locId), ent, user);
        else
            _popup.PopupEntity(Loc.GetString(locId), ent);
    }
}
