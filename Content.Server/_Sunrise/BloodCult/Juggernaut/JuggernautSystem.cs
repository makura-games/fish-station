using Content.Server.Hands.Systems;
using Content.Shared.Body.Events;
using Content.Shared.Timing;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._Sunrise.BloodCult.Juggernaut;

public sealed class JuggernautSystem : EntitySystem
{
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JuggernautComponent, BodyInitEvent>(OnBodyInit);
        // Отдельный маркер — нельзя второй раз подписаться на MeleeThrowOnHitComponent + MeleeHitEvent.
        SubscribeLocalEvent<JuggernautHammerComponent, MeleeHitEvent>(OnHammerMeleeHit);
    }

    private void OnBodyInit(EntityUid uid, JuggernautComponent component, BodyInitEvent args)
    {
        var hammer = Spawn(component.HummerSpawnId, Transform(uid).Coordinates);
        EnsureComp<JuggernautHammerComponent>(hammer);
        _handsSystem.TryForcePickupAnyHand(uid, hammer);
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
