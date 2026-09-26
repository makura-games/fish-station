using Content.Shared._Fish.Objectives.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Maps;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Tag;
using Content.Shared.Warps;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Objectives.Systems;

public sealed partial class AnimalObjectiveTrackerSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> PaperTag = "Paper";
    private static readonly TimeSpan LocationScanInterval = TimeSpan.FromSeconds(1);
    private const float LocationVisitRange = 6f;

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private TagSystem _tag = default!;

    private EntityQuery<EdibleComponent> _edibleQuery;
    private TimeSpan _nextLocationScan;

    public override void Initialize()
    {
        base.Initialize();

        _edibleQuery = GetEntityQuery<EdibleComponent>();
        SubscribeLocalEvent<AnimalObjectiveTrackerComponent, IngestingEvent>(OnIngesting);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateMovement();

        if (_timing.CurTime < _nextLocationScan)
            return;

        _nextLocationScan = _timing.CurTime + LocationScanInterval;
        UpdateVisitedLocations();
    }

    private void UpdateMovement()
    {
        var query = EntityQueryEnumerator<AnimalObjectiveTrackerComponent, TransformComponent>();
        while (query.MoveNext(out _, out var tracker, out var xform))
        {
            if (xform.GridUid is not { } grid)
                continue;

            if (_turf.GetTileRef(xform.Coordinates) is not { } tileRef)
                continue;

            var tile = tileRef.GridIndices;

            if (tracker.LastGrid == grid && tracker.LastTile == tile)
                continue;

            if (tracker.LastTile.HasValue)
                tracker.TilesMoved++;

            tracker.LastGrid = grid;
            tracker.LastTile = tile;
        }
    }

    /// <summary>
    /// Посещение именованных WarpPoint — тот же источник «мест станции», что у spider charge.
    /// </summary>
    private void UpdateVisitedLocations()
    {
        var animalQuery = EntityQueryEnumerator<AnimalObjectiveTrackerComponent, TransformComponent>();
        while (animalQuery.MoveNext(out var animalUid, out var tracker, out var animalXform))
        {
            var warpQuery = EntityQueryEnumerator<WarpPointComponent, TransformComponent>();
            while (warpQuery.MoveNext(out var warpUid, out var warp, out var warpXform))
            {
                if (string.IsNullOrWhiteSpace(warp.Location))
                    continue;

                if (animalXform.MapID != warpXform.MapID)
                    continue;

                if (!_transform.InRange((animalUid, animalXform), (warpUid, warpXform), LocationVisitRange))
                    continue;

                tracker.VisitedLocations.Add(warp.Location);
            }
        }
    }

    private void OnIngesting(Entity<AnimalObjectiveTrackerComponent> ent, ref IngestingEvent args)
    {
        var tracker = ent.Comp;
        var food = args.Food;
        var isDrink = _edibleQuery.TryComp(food, out var edible) && edible.Edible == IngestionSystem.Drink;

        // Реагенты/объём напитков — только для Drink, иначе еда с Coffee засчитывает drink-цели.
        if (isDrink)
        {
            foreach (var (reagent, quantity) in args.Split.Contents)
            {
                var id = reagent.Prototype;
                tracker.DrunkReagents.TryGetValue(id, out var existing);
                tracker.DrunkReagents[id] = existing + quantity;
            }

            tracker.DrinkVolume += args.Split.Volume;
            return;
        }

        tracker.EatCount++;

        if (MetaData(food).EntityPrototype is { } foodProto)
        {
            tracker.EatenFoodProtos.Add(foodProto.ID);
            IncrementFoodParentCounts(tracker, foodProto);
        }

        if (TryComp<TagComponent>(food, out var tagComp))
        {
            foreach (var tag in tagComp.Tags)
            {
                tracker.EatenTagCounts.TryGetValue(tag, out var count);
                tracker.EatenTagCounts[tag] = count + 1;
            }
        }

        if (!_tag.HasTag(food, PaperTag))
            return;

        if (IsBlankPaper(food))
            tracker.BlankPaperEaten++;

        tracker.PaperEaten++;
    }

    private void IncrementFoodParentCounts(AnimalObjectiveTrackerComponent tracker, EntityPrototype foodProto)
    {
        var ancestors = new HashSet<ProtoId<EntityPrototype>>();
        CollectAncestors(foodProto, ancestors);

        foreach (var ancestorId in ancestors)
        {
            tracker.EatenFoodParentCounts.TryGetValue(ancestorId, out var count);
            tracker.EatenFoodParentCounts[ancestorId] = count + 1;
        }
    }

    private void CollectAncestors(EntityPrototype prototype, HashSet<ProtoId<EntityPrototype>> ancestors)
    {
        if (!ancestors.Add(prototype.ID) || prototype.Parents == null)
            return;

        foreach (var parentId in prototype.Parents)
        {
            if (!_proto.TryIndex(parentId, out EntityPrototype? parent))
                continue;

            CollectAncestors(parent, ancestors);
        }
    }

    private bool IsBlankPaper(EntityUid paper)
    {
        if (!TryComp<PaperComponent>(paper, out var paperComp))
            return _tag.HasTag(paper, PaperTag);

        return paperComp.StampedBy.Count == 0 && string.IsNullOrWhiteSpace(paperComp.Content);
    }
}
