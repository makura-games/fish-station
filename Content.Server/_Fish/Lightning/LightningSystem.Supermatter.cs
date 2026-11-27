using System.Linq;
using Content.Server.Lightning.Components;
using Robust.Shared.Random;

namespace Content.Server.Lightning;

public sealed partial class LightningSystem
{
    private void PrioritizeTargets(List<Entity<LightningTargetComponent>> targets)
    {
        _random.Shuffle(targets);
        targets.Sort((first, second) => second.Comp.Priority.CompareTo(first.Comp.Priority));
    }

    /// <summary>Выбирает цель для одиночного разряда с предварительной зарядкой.</summary>
    public bool TrySelectLightningTarget(EntityUid source, float range, out EntityUid target)
    {
        var origin = _transform.GetMapCoordinates(source);
        var targets = _lookup.GetEntitiesInRange<LightningTargetComponent>(origin, range).ToList();
        PrioritizeTargets(targets);
        foreach (var candidate in targets)
        {
            var destination = _transform.GetMapCoordinates(candidate.Owner);
            if (origin.Position == destination.Position || !origin.InRange(destination, range) ||
                !_random.Prob(candidate.Comp.HitProbability))
                continue;

            target = candidate.Owner;
            return true;
        }

        target = default;
        return false;
    }
}
