using Content.Shared._Fish.Rpu;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Spillable;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

using System.Linq;
using Robust.Shared.Enums;
using Robust.Shared.Audio.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Gibbing;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Throwing;
using Content.Shared.Wires;
using Content.Shared.Slippery;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;
namespace Content.Server._Fish.Rpu;

/// <summary>
/// Разливание опасных веществ: лекарства, наркотики, токсины, пиротехника.
/// Химики, медики, сервис и уборщик — на них не действует (их работа проливать).
/// Пара (SpillableComponent, SpillDoAfterEvent) занята лужами, поэтому подписываемся
/// по DrainableSolutionComponent: вылить содержимое можно только из того, что сливается.
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

        // До луж: нам нужно содержимое сосуда до того, как его выльют.
        SubscribeLocalEvent<DrainableSolutionComponent, SpillDoAfterEvent>(OnSpill,
            before: [typeof(Content.Server.Fluids.EntitySystems.PuddleSystem)]);
    }

    private void OnSpill(Entity<DrainableSolutionComponent> ent, ref SpillDoAfterEvent args)
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
        if (threshold <= FixedPoint2.Zero)
        {
            // Нулевой или отрицательный порог делать деление не на что: считаем
            // опасным любой разлив и не пытаемся вынести за него очки.
            if (hazard <= FixedPoint2.Zero)
                return;

            _rpu.Penalize(session, RpuPrototypeIds.SpillHazard, details: "hazard spill");
            return;
        }

        if (hazard < threshold)
            return;

        // Сверх порога — по очку за каждые столько единиц, сколько сам порог (60u), максимум +4.
        var over = (float)(hazard - threshold) / (float)threshold;
        if (!float.IsFinite(over))
            return;

        var extra = Math.Min(4f, (float)Math.Floor(over));

        _rpu.Penalize(session, RpuPrototypeIds.SpillHazard, extra: extra, details: "hazard spill");
    }
}
