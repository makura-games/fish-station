using Content.Server._Fish.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Shared._Fish.Objectives.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Warps;

namespace Content.Server._Fish.Objectives.Systems;

public sealed partial class AnimalObjectiveConditionsSystem : EntitySystem
{
    [Dependency] private NumberObjectiveSystem _number = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnimalEatCountConditionComponent, ObjectiveGetProgressEvent>(OnEatCount);
        SubscribeLocalEvent<AnimalDrinkVolumeConditionComponent, ObjectiveGetProgressEvent>(OnDrinkVolume);
        SubscribeLocalEvent<AnimalDrinkReagentConditionComponent, ObjectiveGetProgressEvent>(OnDrinkReagent);
        SubscribeLocalEvent<AnimalEatFoodConditionComponent, ObjectiveGetProgressEvent>(OnEatFood);
        SubscribeLocalEvent<AnimalEatPaperConditionComponent, ObjectiveGetProgressEvent>(OnEatPaper);
        SubscribeLocalEvent<AnimalTileDistanceConditionComponent, ObjectiveGetProgressEvent>(OnTileDistance);
        SubscribeLocalEvent<AnimalVisitLocationsConditionComponent, RequirementCheckEvent>(OnVisitLocationsRequirement);
        // После NumberObjectiveSystem — Target уже выбран.
        SubscribeLocalEvent<AnimalVisitLocationsConditionComponent, ObjectiveAssignedEvent>(
            OnVisitLocationsAssigned,
            after: [typeof(NumberObjectiveSystem)]);
        SubscribeLocalEvent<AnimalVisitLocationsConditionComponent, ObjectiveGetProgressEvent>(OnVisitLocations);
        SubscribeLocalEvent<AnimalTryNewFoodConditionComponent, ObjectiveGetProgressEvent>(OnTryNewFood);
    }

    private void OnEatCount(EntityUid uid, AnimalEatCountConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetTracker(args) is { } tracker
            ? GetProgress(tracker.EatCount, _number.GetTarget(uid))
            : 0f;
    }

    private void OnDrinkVolume(EntityUid uid, AnimalDrinkVolumeConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetTracker(args) is { } tracker
            ? GetProgress((float) tracker.DrinkVolume, _number.GetTarget(uid))
            : 0f;
    }

    private void OnDrinkReagent(EntityUid uid, AnimalDrinkReagentConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (GetTracker(args) is not { } tracker)
        {
            args.Progress = 0f;
            return;
        }

        var volume = tracker.DrunkReagents.GetValueOrDefault(comp.Reagent);

        foreach (var reagent in comp.AlsoReagents)
            volume += tracker.DrunkReagents.GetValueOrDefault(reagent);

        args.Progress = GetProgress((float) volume, _number.GetTarget(uid));
    }

    private void OnEatFood(EntityUid uid, AnimalEatFoodConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (GetTracker(args) is not { } tracker)
        {
            args.Progress = 0f;
            return;
        }

        var current = comp.Tag is { } tag
            ? tracker.EatenTagCounts.GetValueOrDefault(tag)
            : comp.FoodParent is { } foodParent
                ? tracker.EatenFoodParentCounts.GetValueOrDefault(foodParent)
                : 0;

        args.Progress = GetProgress(current, _number.GetTarget(uid));
    }

    private void OnEatPaper(EntityUid uid, AnimalEatPaperConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (GetTracker(args) is not { } tracker)
        {
            args.Progress = 0f;
            return;
        }

        var current = comp.RequireBlank ? tracker.BlankPaperEaten : tracker.PaperEaten;
        args.Progress = GetProgress(current, _number.GetTarget(uid));
    }

    private void OnTileDistance(EntityUid uid, AnimalTileDistanceConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetTracker(args) is { } tracker
            ? GetProgress(tracker.TilesMoved, _number.GetTarget(uid))
            : 0f;
    }

    private void OnVisitLocationsRequirement(EntityUid uid, AnimalVisitLocationsConditionComponent comp, ref RequirementCheckEvent args)
    {
        if (args.Cancelled)
            return;

        // Как spider charge: без именованных мест цель не выдаём.
        // Считаем уникальные Location — прогресс тоже по уникальным строкам.
        if (CountUniqueNamedWarpLocations() == 0)
            args.Cancelled = true;
    }

    private void OnVisitLocationsAssigned(EntityUid uid, AnimalVisitLocationsConditionComponent comp, ref ObjectiveAssignedEvent args)
    {
        if (args.Cancelled)
            return;

        var unique = CountUniqueNamedWarpLocations();
        if (_number.GetTarget(uid) > unique)
            args.Cancelled = true;
    }

    private void OnVisitLocations(EntityUid uid, AnimalVisitLocationsConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetTracker(args) is { } tracker
            ? GetProgress(tracker.VisitedLocations.Count, _number.GetTarget(uid))
            : 0f;
    }

    private void OnTryNewFood(EntityUid uid, AnimalTryNewFoodConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetTracker(args) is { } tracker
            ? GetProgress(tracker.EatenFoodProtos.Count, _number.GetTarget(uid))
            : 0f;
    }

    private int CountUniqueNamedWarpLocations()
    {
        var locations = new HashSet<string>();
        var query = EntityQueryEnumerator<WarpPointComponent>();
        while (query.MoveNext(out _, out var warp))
        {
            if (!string.IsNullOrWhiteSpace(warp.Location))
                locations.Add(warp.Location);
        }

        return locations.Count;
    }

    private AnimalObjectiveTrackerComponent? GetTracker(ObjectiveGetProgressEvent args)
    {
        if (args.Mind.OwnedEntity is not { } entity || !TryComp(entity, out AnimalObjectiveTrackerComponent? tracker))
            return null;

        return tracker;
    }

    private static float GetProgress(int current, int target)
    {
        if (target <= 0)
            return 1f;

        return Math.Clamp((float) current / target, 0f, 1f);
    }

    private static float GetProgress(float current, float target)
    {
        if (target <= 0f)
            return 1f;

        return Math.Clamp(current / target, 0f, 1f);
    }
}
