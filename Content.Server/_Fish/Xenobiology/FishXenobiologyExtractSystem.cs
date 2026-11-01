using Content.Shared._Fish.Xenobiology;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Xenobiology;

/// <summary>Consumes extracts and dormant cultures once, and applies bounded genetic treatments.</summary>
public sealed partial class FishXenobiologyExtractSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private FishXenobiologySystem _xenobiology = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;

    private static readonly EntProtoId SampleBottle = "FishXenoSampleBottle";
    private static readonly FixedPoint2 HydrationWater = FixedPoint2.New(5);

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishXenobiologyExtractComponent, SolutionChangedEvent>(OnExtractSolutionChanged);
        SubscribeLocalEvent<FishXenobiologyExtractComponent, ExaminedEvent>(OnExtractExamined);
        SubscribeLocalEvent<FishXenobiologyCultureComponent, SolutionChangedEvent>(OnCultureSolutionChanged);
        SubscribeLocalEvent<FishXenobiologyCultureComponent, EntGotRemovedFromContainerMessage>(OnCultureRemoved);
        SubscribeLocalEvent<FishXenobiologySerumComponent, AfterInteractEvent>(OnSerumAfterInteract);
    }

    private void OnExtractSolutionChanged(Entity<FishXenobiologyExtractComponent> ent, ref SolutionChangedEvent args)
    {
        if (args.Solution.Comp.Id == ent.Comp.Solution)
            TryReact(ent);
    }

    private void OnExtractExamined(Entity<FishXenobiologyExtractComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Used ? "fish-xeno-extract-spent" : "fish-xeno-extract-ready"));
    }

    private void OnCultureSolutionChanged(Entity<FishXenobiologyCultureComponent> ent, ref SolutionChangedEvent args)
    {
        if (args.Solution.Comp.Id == ent.Comp.Solution)
            TryHatch(ent);
    }

    private void OnCultureRemoved(Entity<FishXenobiologyCultureComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        TryHatch(ent);
    }

    private void OnSerumAfterInteract(Entity<FishXenobiologySerumComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = TryApplySerum(ent, args.User, target);
    }

    /// <summary>Claims an unused extract before chemistry callbacks or output spawns can re-enter this system.</summary>
    public bool TryReact(Entity<FishXenobiologyExtractComponent> ent)
    {
        if (!CanReact(ent, out var reaction, out var solution, out var coordinates))
            return false;

        // Fish-edit: изменение раствора вызывает вложенный SolutionChangedEvent, поэтому сначала расходуем экстракт.
        ent.Comp.Used = true;
        _solutionContainer.RemoveReagent(solution, reaction.Reagent.Id, reaction.Amount);

        foreach (var (product, count) in reaction.Products)
        {
            for (var i = 0; i < count; i++)
                Spawn(product, coordinates);
        }

        if (reaction.Reagents.Count > 0)
        {
            var bottle = Spawn(SampleBottle, coordinates);
            if (_solutionContainer.TryGetSolution(bottle, "drink", out var bottleSolution, out _))
            {
                var reagents = new Solution();
                foreach (var (reagent, quantity) in reaction.Reagents)
                    reagents.AddReagent(reagent.Id, quantity);

                _solutionContainer.TryAddSolution(bottleSolution.Value, reagents);
            }
        }

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(ent)} reacted using xenobiology recipe {reaction.ID}.");
        _popup.PopupEntity(Loc.GetString("fish-xeno-extract-reacted", ("result", Loc.GetString(reaction.Name))), ent);
        return true;
    }

    /// <summary>Checks catalyst quantity and world coordinates without consuming the extract.</summary>
    public bool CanReact(Entity<FishXenobiologyExtractComponent> ent,
        out FishXenobiologyReactionPrototype reaction,
        out Entity<SolutionComponent> solution,
        out EntityCoordinates coordinates)
    {
        reaction = default!;
        solution = default;
        coordinates = default;
        if (TerminatingOrDeleted(ent) || ent.Comp.Used ||
            !_solutionContainer.TryGetSolution(ent.Owner, ent.Comp.Solution, out var solutionEntity, out var mixture) ||
            !_transform.TryGetMapOrGridCoordinates(ent, out var outputCoordinates))
            return false;

        foreach (var candidate in _prototype.EnumeratePrototypes<FishXenobiologyReactionPrototype>())
        {
            if (candidate.Breed != ent.Comp.Breed || candidate.Amount <= FixedPoint2.Zero ||
                mixture.GetTotalPrototypeQuantity(candidate.Reagent) < candidate.Amount)
                continue;

            if (reaction == null || string.CompareOrdinal(candidate.ID, reaction.ID) < 0)
                reaction = candidate;
        }

        if (reaction == null)
            return false;

        solution = solutionEntity.Value;
        coordinates = outputCoordinates.Value;
        return true;
    }

    /// <summary>Hatches a hydrated culture on the floor, respecting the laboratory population limit.</summary>
    public bool TryHatch(Entity<FishXenobiologyCultureComponent> ent)
    {
        if (!CanHatch(ent, out var breed, out var coordinates))
            return false;

        ent.Comp.Used = true;
        var slime = Spawn(breed.Mob, coordinates);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(ent)} hatched into {ToPrettyString(slime)}.");
        _popup.PopupEntity(Loc.GetString("fish-xeno-culture-hatched"), slime);
        QueueDel(ent);
        return true;
    }

    /// <summary>Checks hydration, containment and population before creating any creature.</summary>
    public bool CanHatch(Entity<FishXenobiologyCultureComponent> ent,
        out FishXenobiologyBreedPrototype breed, out EntityCoordinates coordinates)
    {
        breed = default!;
        coordinates = default;
        if (TerminatingOrDeleted(ent) || ent.Comp.Used || _container.IsEntityInContainer(ent.Owner) ||
            !_prototype.TryIndex(ent.Comp.Breed, out var cultureBreed) ||
            !_solutionContainer.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution) ||
            solution.GetTotalPrototypeQuantity("Water") < HydrationWater ||
            !_transform.TryGetMapOrGridCoordinates(ent, out var outputCoordinates))
            return false;

        if (_turf.GetTileRef(outputCoordinates.Value) is not { } tile || _turf.IsSpace(tile))
            return false;

        var count = 0;
        var map = Transform(ent).MapUid;
        var query = EntityQueryEnumerator<FishXenobiologySlimeComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapUid == map && ++count >= FishXenobiologySystem.PopulationLimit)
                return false;
        }

        breed = cultureBreed;
        coordinates = outputCoordinates.Value;
        return true;
    }

    /// <summary>Applies one heritable treatment through an actual held, reachable interaction.</summary>
    public bool TryApplySerum(Entity<FishXenobiologySerumComponent> ent, EntityUid user, EntityUid target)
    {
        if (!CanApplySerum(ent, user, target))
            return false;

        if (!_xenobiology.TryApplyTreatment(target, ent.Comp.Treatment))
        {
            _popup.PopupEntity(Loc.GetString("fish-xeno-serum-limit"), target, user);
            return false;
        }

        ent.Comp.Used = true;
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user)} applied {ent.Comp.Treatment} treatment to {ToPrettyString(target)}.");
        _popup.PopupEntity(Loc.GetString("fish-xeno-serum-applied"), target, user);
        QueueDel(ent);
        return true;
    }

    /// <summary>Checks the source, recipient, reach and ability to interact without changing either entity.</summary>
    public bool CanApplySerum(Entity<FishXenobiologySerumComponent> ent, EntityUid user, EntityUid target)
    {
        return !TerminatingOrDeleted(ent) && !TerminatingOrDeleted(user) && !TerminatingOrDeleted(target) &&
               !ent.Comp.Used && HasComp<FishXenobiologySlimeComponent>(target) &&
               _hands.IsHolding(user, ent) && _actionBlocker.CanInteract(user, target) &&
               _interaction.InRangeUnobstructed(user, target);
    }
}
