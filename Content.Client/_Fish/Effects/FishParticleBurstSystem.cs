using System.Numerics;
using Content.Shared._Fish.Effects;
using Content.Shared.CCVar;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Effects;

public sealed partial class FishParticleBurstSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const int MaxParticles = 96;
    private bool _enabled;
    private readonly List<Particle> _active = new(MaxParticles);
    private readonly record struct Particle(EntityUid Entity, Vector2 Velocity, float Gravity, FishParticleKind Kind, TimeSpan Start);

    public override void Initialize()
    {
        Subs.CVar(_cfg, FishCVars.ActionParticlesEnabled, SetEnabled, true);
        SubscribeNetworkEvent<FishParticleBurstEvent>(OnBurst);
    }

    private void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (enabled)
            return;

        foreach (var particle in _active)
            TryQueueDel(particle.Entity);

        _active.Clear();
    }

    private void OnBurst(FishParticleBurstEvent ev)
    {
        if (!_enabled || _active.Count >= MaxParticles)
            return;

        var position = GetCoordinates(ev.Position);
        if (!position.IsValid(EntityManager))
            return;

        // Скорости и ускорение заданы в пикселях спрайта за секунду и за секунду в квадрате.
        var (prototype, count, spread, minRise, maxRise, gravity) = ev.Kind switch
        {
            FishParticleKind.BloodSpray => ("FishBloodParticle", 6, 32f, 12f, 38f, 140f),
            FishParticleKind.BloodDrip => ("FishBloodParticle", 2, 8f, -5f, 4f, 92f),
            FishParticleKind.Weld => ("FishWeldParticle", 8, 40f, 20f, 47f, 154f),
            FishParticleKind.Slag => ("FishSlagParticle", 5, 24f, 8f, 30f, 128f),
            FishParticleKind.Splash => ("FishSplashParticle", 5, 26f, 12f, 32f, 130f),
            FishParticleKind.Ignition => ("FishIgnitionParticle", 6, 16f, 14f, 28f, -10f),
            FishParticleKind.Cinders => ("FishCindersParticle", 3, 10f, 12f, 24f, -6f),
            FishParticleKind.Smoke => ("FishSmokeParticle", 4, 12f, 8f, 14f, -16f),
            FishParticleKind.Frost => ("FishFrostParticle", 5, 20f, 0f, 8f, 22f),
            FishParticleKind.Steam => ("FishSteamParticle", 4, 7f, 12f, 18f, -12f),
            FishParticleKind.Leaves => ("FishLeavesParticle", 5, 20f, 14f, 22f, 62f),
            FishParticleKind.Ore => ("FishOreParticle", 6, 32f, 14f, 34f, 142f),
            FishParticleKind.Glass => ("FishGlassParticle", 7, 35f, 12f, 36f, 148f),
            FishParticleKind.Wood => ("FishWoodParticle", 6, 32f, 10f, 30f, 118f),
            FishParticleKind.Shock => ("FishShockParticle", 4, 28f, 8f, 22f, 60f),
            FishParticleKind.Suction => ("FishSuctionParticle", 7, 34f, 15f, 34f, 0f),
            FishParticleKind.Bubbles => ("FishBubblesParticle", 4, 8f, 10f, 20f, -12f),
            _ => throw new ArgumentOutOfRangeException(nameof(ev.Kind)),
        };

        for (var i = 0; i < count && _active.Count < MaxParticles; i++)
        {
            var entity = SpawnAttachedTo(prototype, position);
            _sprites.SetColor(entity, ev.Color);
            var velocity = new Vector2(_random.NextFloat(-spread, spread), _random.NextFloat(minRise, maxRise));
            if (ev.Kind == FishParticleKind.Suction && _random.Prob(0.5f))
                velocity.Y = -velocity.Y;
            _active.Add(new Particle(entity, velocity, gravity, ev.Kind, _timing.CurTime));
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        var now = _timing.CurTime;
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var particle = _active[i];
            if (!TryComp<SpriteComponent>(particle.Entity, out var sprite))
            {
                _active.RemoveAt(i);
                continue;
            }

            var t = (float) (now - particle.Start).TotalSeconds;
            var pixels = particle.Velocity * t - new Vector2(0, particle.Gravity * t * t / 2);
            // Пыль идёт от края к центру, а лёгкие частицы качаются в потоке воздуха.
            if (particle.Kind == FishParticleKind.Suction)
                pixels = particle.Velocity * Math.Max(0, 0.48f - t);
            else if (particle.Kind is FishParticleKind.Leaves or FishParticleKind.Cinders or FishParticleKind.Smoke or FishParticleKind.Steam)
                pixels.X += 3 * (MathF.Sin(10 * t + particle.Velocity.X) - MathF.Sin(particle.Velocity.X));

            var offset = new Vector2(MathF.Round(pixels.X), MathF.Round(pixels.Y)) / 32f;
            if (sprite.Offset != offset)
                _sprites.SetOffset((particle.Entity, sprite), offset);
        }
    }
}
