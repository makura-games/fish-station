using Content.Server.Atmos.EntitySystems;
using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._Fish.Xenobiology;
using Content.Shared.Atmos.Components;
using Content.Shared.Interaction;
using Content.Shared.Stunnable;
using Content.Shared.Temperature.Components;
using Content.Shared.Temperature.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server._Fish.Xenobiology;

public sealed partial class FishXenobiologyReactionSystem
{
    // Fish-edit: эффекты используют штатные системы урона, температур, статусов и взрывов.
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private SharedTemperatureSystem _temperature = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private EmpSystem _emp = default!;
    [Dependency] private ExplosionSystem _explosion = default!;

    private void ApplyEffect(EntityUid source, FishXenobiologyReactionPrototype recipe,
        MapCoordinates coordinates, EntityUid? user)
    {
        switch (recipe.Effect)
        {
            case FishXenobiologyReactionEffect.Emp:
                _emp.EmpPulse(coordinates, recipe.Radius, recipe.Strength, recipe.Duration, user);
                return;
            case FishXenobiologyReactionEffect.Explosion:
                _explosion.QueueExplosion(source, "Default", recipe.Strength, 4f, 20f, user: user);
                return;
            case FishXenobiologyReactionEffect.Spawn:
                Summon(source, recipe);
                return;
        }

        foreach (var target in _lookup.GetEntitiesInRange(coordinates, recipe.Radius))
        {
            if (TerminatingOrDeleted(target) || _container.IsEntityOrParentInContainer(target) ||
                !_interaction.InRangeUnobstructed(coordinates, _transform.GetMapCoordinates(target),
                    recipe.Radius, predicate: entity => entity == source || entity == target))
                continue;

            switch (recipe.Effect)
            {
                case FishXenobiologyReactionEffect.Fire:
                    if (TryComp<FlammableComponent>(target, out var flammable))
                    {
                        _flammable.AdjustFireStacks(target, recipe.Strength, flammable);
                        _flammable.Ignite(target, source, flammable, user);
                    }
                    break;
                case FishXenobiologyReactionEffect.Cold:
                    if (TryComp<TemperatureComponent>(target, out var temperature))
                    {
                        var delta = Math.Min(0f, Math.Max(173.15f, temperature.Temperature - recipe.Strength) - temperature.Temperature);
                        _temperature.ChangeHeat((target, temperature), delta * temperature.HeatCapacity);
                    }
                    break;
                case FishXenobiologyReactionEffect.Stasis:
                    if (_mobState.IsAlive(target))
                        _stun.TryUpdateParalyzeDuration(target, recipe.Duration);
                    break;
            }
        }
    }
}
