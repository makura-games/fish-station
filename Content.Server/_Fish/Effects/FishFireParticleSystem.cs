using Content.Shared._Fish.Effects;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Electrocution;

namespace Content.Server._Fish.Effects;

public sealed partial class FishFireParticleSystem : EntitySystem
{
    [Dependency] private FishActionParticleSystem _fishActionParticle = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FlammableComponent, IgnitedEvent>(OnIgnited);
        SubscribeLocalEvent<FlammableComponent, ExtinguishedEvent>(OnExtinguished);
        SubscribeLocalEvent<ElectrocutedEvent>(ev => _fishActionParticle.Emit(ev.TargetUid, FishParticleKind.Shock));
    }

    private void OnIgnited(Entity<FlammableComponent> ent, ref IgnitedEvent args)
        => _fishActionParticle.Emit(ent, FishParticleKind.Ignition);

    private void OnExtinguished(Entity<FlammableComponent> ent, ref ExtinguishedEvent args)
        => _fishActionParticle.Emit(ent, FishParticleKind.Smoke);
}
