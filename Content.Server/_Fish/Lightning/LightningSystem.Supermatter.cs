using System.Linq;
using Content.Server.Lightning.Components;
using Robust.Shared.Random;

namespace Content.Server.Lightning;

public sealed partial class LightningSystem
{
    private List<Entity<LightningTargetComponent>> GetPrioritizedTargets(EntityUid source, float range)
    {
        var targets = _lookup.GetEntitiesInRange<LightningTargetComponent>(_transform.GetMapCoordinates(source), range).ToList();
        _random.Shuffle(targets);
        targets.Sort((first, second) => second.Comp.Priority.CompareTo(first.Comp.Priority));
        return targets;
    }

    /// <summary>Выбирает цель для одиночного разряда с предварительной зарядкой.</summary>
    public bool TrySelectLightningTarget(EntityUid source, float range, out EntityUid target)
    {
        var origin = _transform.GetMapCoordinates(source);
        foreach (var candidate in GetPrioritizedTargets(source, range))
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
