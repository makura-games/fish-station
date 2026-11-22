using System.Numerics;
using Content.Shared._Fish.Effects;
using Robust.Client.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Effects;

public sealed partial class FishParticleBurstSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const int MaxParticles = 96;
    private readonly List<Particle> _active = new(MaxParticles);
    private readonly record struct Particle(EntityUid Entity, Vector2 Velocity, float Gravity, TimeSpan Start);

    public override void Initialize()
    {
        SubscribeNetworkEvent<FishParticleBurstEvent>(OnBurst);
    }

    private void OnBurst(FishParticleBurstEvent ev)
    {
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
            _ => throw new ArgumentOutOfRangeException(nameof(ev.Kind)),
        };

        for (var i = 0; i < count && _active.Count < MaxParticles; i++)
        {
            var entity = SpawnAttachedTo(prototype, position);
            _sprites.SetColor(entity, ev.Color);
            var velocity = new Vector2(_random.NextFloat(-spread, spread), _random.NextFloat(minRise, maxRise));
            _active.Add(new Particle(entity, velocity, gravity, _timing.CurTime));
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var particle = _active[i];
            if (!TryComp<SpriteComponent>(particle.Entity, out var sprite))
            {
                _active.RemoveAt(i);
                continue;
            }

            var t = (float) (_timing.CurTime - particle.Start).TotalSeconds;
            var pixels = particle.Velocity * t - new Vector2(0, particle.Gravity * t * t / 2);
            _sprites.SetOffset((particle.Entity, sprite), new Vector2(MathF.Round(pixels.X), MathF.Round(pixels.Y)) / 32f);
        }
    }
}
