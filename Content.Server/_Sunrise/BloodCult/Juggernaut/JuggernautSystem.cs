using Content.Server.Hands.Systems;
using Content.Shared.Timing.Systems;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._Sunrise.BloodCult.Juggernaut;

public sealed partial class JuggernautSystem : EntitySystem
{
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JuggernautComponent, MapInitEvent>(OnMapInit, after: [typeof(HandsSystem)]);
        // Отдельный маркер — нельзя второй раз подписаться на MeleeThrowOnHitComponent + MeleeHitEvent.
        SubscribeLocalEvent<JuggernautHammerComponent, MeleeHitEvent>(OnHammerMeleeHit);
    }

    private void OnMapInit(Entity<JuggernautComponent> ent, ref MapInitEvent args)
    {
        var hammer = Spawn(ent.Comp.HummerSpawnId, Transform(ent).Coordinates);
        EnsureComp<JuggernautHammerComponent>(hammer);
        _hands.TryForcePickupAnyHand(ent, hammer);
    }

    private void OnHammerMeleeHit(EntityUid weapon, JuggernautHammerComponent component, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        if (!HasComp<JuggernautComponent>(args.User))
            return;

        _useDelay.TryResetDelay(weapon);
    }
}
