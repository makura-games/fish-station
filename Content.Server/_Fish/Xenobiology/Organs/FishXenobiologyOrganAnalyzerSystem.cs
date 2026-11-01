using Content.Server.Power.EntitySystems;
using Content.Server.Research.Systems;
using Content.Shared._Fish.Xenobiology.Organs;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Xenobiology.Organs;

/// <summary>
/// Анализатор: оживлённый орган, до 15 с, очки на ближайший research server.
/// </summary>
public sealed partial class FishXenobiologyOrganAnalyzerSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishXenobiologyOrganAnalyzerComponent, InteractUsingEvent>(OnInteractUsing);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FishXenoOrganAnalyzerScanComponent, FishXenobiologyOrganAnalyzerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var scan, out var analyzer, out var xform))
        {
            if (_timing.CurTime < scan.FinishAt)
                continue;

            CompleteScan(uid, scan, analyzer, xform);
        }
    }

    private void OnInteractUsing(Entity<FishXenobiologyOrganAnalyzerComponent> analyzer, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<FishXenoOrganComponent>(args.Used, out var organ))
            return;

        if (!organ.Identified)
        {
            _popup.PopupEntity(Loc.GetString("fish-xeno-analyzer-unidentified"), args.User, args.User);
            return;
        }

        if (organ.Analyzed)
        {
            _popup.PopupEntity(Loc.GetString("fish-xeno-analyzer-done"), args.User, args.User);
            return;
        }

        if (HasComp<FishXenoOrganAnalyzerScanComponent>(analyzer))
        {
            _popup.PopupEntity(Loc.GetString("fish-xeno-analyzer-busy"), args.User, args.User);
            return;
        }

        args.Handled = true;
        var scan = EnsureComp<FishXenoOrganAnalyzerScanComponent>(analyzer);
        scan.Organ = args.Used;
        scan.FinishAt = _timing.CurTime + analyzer.Comp.ScanDuration;
        Dirty(analyzer, scan);
        _popup.PopupEntity(Loc.GetString("fish-xeno-analyzer-start"), analyzer, args.User);
    }

    private void CompleteScan(
        EntityUid analyzerUid,
        FishXenoOrganAnalyzerScanComponent scan,
        FishXenobiologyOrganAnalyzerComponent analyzer,
        TransformComponent xform)
    {
        RemComp<FishXenoOrganAnalyzerScanComponent>(analyzerUid);

        if (!Exists(scan.Organ) || !TryComp<FishXenoOrganComponent>(scan.Organ, out var organ))
            return;

        organ.Analyzed = true;
        Dirty(scan.Organ, organ);

        var mult = organ.Quality switch
        {
            FishXenoOrganQuality.Pristine => 2f,
            FishXenoOrganQuality.Damaged => 0.5f,
            _ => 1f,
        };

        var points = (int) (analyzer.BasePoints * mult);
        var coords = _transform.GetMapCoordinates(analyzerUid, xform);
        if (TryFindServer(coords) is { } server)
            _research.ModifyServerPoints(server, points);

        _popup.PopupEntity(Loc.GetString("fish-xeno-analyzer-finished", ("points", points)), analyzerUid);
    }

    private EntityUid? TryFindServer(MapCoordinates coords)
    {
        foreach (var uid in _lookup.GetEntitiesInRange<ResearchServerComponent>(coords, 15f))
        {
            if (this.IsPowered(uid, EntityManager))
                return uid;
        }

        return null;
    }
}
