using Content.Server.Lightning.Components;
using Content.Shared._Fish.Effects;
using Content.Shared.Starlight.Energy.Supermatter;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Starlight.Energy.Supermatter;

public sealed partial class SupermatterSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, (EntityUid Target, TimeSpan FireAt)> _charging = new();

    private void EmitCrystalParticles(EntityUid crystal, FishSupermatterParticleEffect effect)
    {
        RaiseNetworkEvent(new FishSupermatterParticleEvent(GetNetEntity(crystal), effect), Filter.Pvs(crystal));
    }

    private void HandleCrystalDamage(Entity<SupermatterComponent> crystal)
    {
        var previous = crystal.Comp.Durability;
        HandleDestruction(crystal);
        if (crystal.Comp.Durability < previous)
            EmitCrystalParticles(crystal.Owner, FishSupermatterParticleEffect.Chip);
    }

    private void BeginDischarge(Entity<SupermatterComponent> crystal)
    {
        if (crystal.Comp.Durability <= 0.01f || crystal.Comp.AccLighting <= 0 || _charging.ContainsKey(crystal.Owner) ||
            !_lightning.TrySelectLightningTarget(crystal.Owner, crystal.Comp.AccLighting.Float(), out var target))
            return;

        _charging.Add(crystal.Owner, (target, _timing.CurTime + TimeSpan.FromSeconds(FishSupermatterParticleEvent.ChargeSeconds)));
        EmitCrystalParticles(crystal.Owner, FishSupermatterParticleEffect.Charge);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var (source, shot) in _charging)
        {
            if (_timing.CurTime < shot.FireAt)
                continue;

            _charging.Remove(source);
            if (!TryComp<SupermatterComponent>(source, out var crystal) || TerminatingOrDeleted(source) ||
                !crystal.Activated || Paused(source) || !HasComp<LightningTargetComponent>(shot.Target) ||
                TerminatingOrDeleted(shot.Target))
                continue;

            // Если во время зарядки цель ушла из радиуса или сменила карту, заряд остаётся в кристалле.
            var from = _transform.GetMapCoordinates(source);
            var to = _transform.GetMapCoordinates(shot.Target);
            if (from.Position == to.Position || !from.InRange(to, crystal.AccLighting.Float()))
                continue;

            _lightning.ShootLightning(source, shot.Target);
            crystal.AccLighting = 0;
        }
    }
}
