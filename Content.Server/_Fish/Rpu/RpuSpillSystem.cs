using Content.Shared._Fish.Rpu;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Spillable;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Разливание опасных веществ: лекарства, наркотики, токсины, пиротехника.
/// Химики, медики, сервис и уборщик — на них не действует (их работа проливать).
/// </summary>
public sealed partial class RpuSpillSystem : EntitySystem
{
    [Dependency] private RpuSystem _rpu = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly HashSet<string> HazardGroups = new()
    {
        "Medicine", "Narcotics", "Toxins", "Pyrotechnic",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpillableComponent, SpillDoAfterEvent>(OnSpill,
            before: [typeof(Content.Server.Fluids.EntitySystems.PuddleSystem)]);
    }

    private void OnSpill(Entity<SpillableComponent> ent, ref SpillDoAfterEvent args)
    {
        if (!_rpu.Active || args.Handled || args.Cancelled)
            return;

        if (CompOrNull<ActorComponent>(args.Args.User)?.PlayerSession is not { } session)
            return;

        if (!_solutions.TryGetDrainableSolution(ent.Owner, out _, out var solution))
            return;

        var hazard = FixedPoint2.Zero;

        foreach (var quantity in solution.Contents)
        {
            if (!_prototypes.TryIndex(quantity.Reagent.Prototype, out var proto))
                continue;

            if (HazardGroups.Contains(proto.Group))
                hazard += quantity.Quantity;
        }

        var threshold = _rpu.SpillThreshold;
        if (hazard < threshold)
            return;

        // Сверх порога штраф растёт каждые 100 единиц.
        var severity = 1f + MathF.Max(0f, (float)(hazard - threshold) / 100f) * 0.5f;

        _rpu.Penalize(session, RpuPrototypeIds.SpillHazard, severity: severity, details: "hazard spill");
    }
}
