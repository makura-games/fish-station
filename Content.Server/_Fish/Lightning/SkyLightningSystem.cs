using Content.Server.Electrocution;
using Content.Shared._Fish.Lightning;
using Content.Shared.StatusEffect;
using Content.Shared.Interaction;
using Content.Shared.Trigger;
using Robust.Shared.Random;
using Robust.Shared.GameStates;
using Robust.Shared.Spawners;

namespace Content.Server._Fish.Lightning;

/// <summary>
/// Применяет электрический удар вокруг точки спавна молнии.
/// </summary>
public sealed class SkyLightningSystem : EntitySystem
{
    [Dependency] private ElectrocutionSystem _electrocution = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPvsOverrideSystem _pvs = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkyLightningComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SkyLightningComponent, TriggerEvent>(OnTrigger);
    }

    private void OnMapInit(Entity<SkyLightningComponent> ent, ref MapInitEvent args)
    {
        // Таймер только на сервере: клиентский предпросмотр не должен удаляться.
        EnsureComp<TimedDespawnComponent>(ent).Lifetime = 1.2f;
        // Высокий разряд виден даже при попадании точки удара за обычную границу PVS.
        _pvs.AddGlobalOverride(ent);
        _appearance.SetData(ent, SkyLightningVisuals.Tilt, _random.NextFloat(-ent.Comp.MaxTilt, ent.Comp.MaxTilt));
        _appearance.SetData(ent, SkyLightningVisuals.Variant, _random.Next(1, 13));
    }

    private void OnTrigger(Entity<SkyLightningComponent> ent, ref TriggerEvent args)
    {
        if (args.Key == ent.Comp.StrikeKey)
            args.Handled |= TryStrike(ent);
    }

    public bool TryStrike(Entity<SkyLightningComponent> ent)
    {
        if (!CanStrike(ent))
            return false;

        Strike(ent);
        return true;
    }

    public bool CanStrike(Entity<SkyLightningComponent> ent)
    {
        return !TerminatingOrDeleted(ent) && !ent.Comp.Struck && ent.Comp.Radius > 0;
    }

    private void Strike(Entity<SkyLightningComponent> ent)
    {
        ent.Comp.Struck = true;

        var coordinates = _transform.GetMapCoordinates(ent);
        foreach (var target in _lookup.GetEntitiesInRange<StatusEffectsComponent>(
                     coordinates, ent.Comp.Radius, LookupFlags.Uncontained))
        {
            if (!_interaction.InRangeUnobstructed(coordinates,
                    _transform.GetMapCoordinates(target),
                    ent.Comp.Radius,
                    predicate: uid => uid == ent.Owner || uid == target.Owner))
                continue;

            _electrocution.TryDoElectrocution(target,
                ent,
                ent.Comp.ShockDamage,
                ent.Comp.StunDuration,
                true,
                statusEffects: target.Comp);
        }
    }
}
