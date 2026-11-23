using System.Numerics;
using Content.Shared._Fish.Effects;
using Content.Shared.CCVar;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Effects;

/// <summary>Локальные клубы дыма и осколки кристалла с движением по пиксельной сетке.</summary>
public sealed partial class FishMachineParticleSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly EntProtoId Smoke = "FishSmokeParticle";
    private static readonly EntProtoId Spark = "FishWeldParticle";
    private static readonly EntProtoId Chip = "FishSupermatterChipParticle";
    private static readonly Color LightSmoke = Color.FromHex("#CDD0D5");
    private static readonly Color HeavySmoke = Color.FromHex("#797F89");
    private const int MaxSmoke = 64;
    private const int MaxChips = 96;
    private readonly Dictionary<EntityUid, TimeSpan> _nextPuff = new();
    private readonly List<Puff> _puffs = new(MaxSmoke);
    private readonly List<Shard> _shards = new(MaxChips);
    private TimeSpan _nextBorgCheck;
    private bool _enabled;

    private readonly record struct Puff(EntityUid Entity, Vector2 Drift, TimeSpan Start, float Gravity = 0f);
    private readonly record struct Shard(EntityUid Entity, EntityUid Crystal, Vector2 From, Vector2 To,
        TimeSpan Start, bool Returning);

    public override void Initialize()
    {
        base.Initialize();
        UpdatesOutsidePrediction = true;
        Subs.CVar(_cfg, FishCVars.ActionParticlesEnabled, SetEnabled, true);
        SubscribeNetworkEvent<FishSupermatterParticleEvent>(OnCrystalEffect);
    }

    private void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (_enabled)
            return;

        foreach (var puff in _puffs)
            TryQueueDel(puff.Entity);
        foreach (var shard in _shards)
            TryQueueDel(shard.Entity);
        _puffs.Clear();
        _shards.Clear();
        _nextPuff.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (!_enabled || !_timing.IsFirstTimePredicted || now < _nextBorgCheck)
            return;

        _nextBorgCheck = now + TimeSpan.FromSeconds(0.1);
        foreach (var (uid, next) in _nextPuff)
        {
            if (next <= now)
                _nextPuff.Remove(uid);
        }
        EmitBorgSmoke(now);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (!_enabled)
            return;

        var now = _timing.CurTime;
        for (var i = _puffs.Count - 1; i >= 0; i--)
        {
            var puff = _puffs[i];
            if (!TryComp<SpriteComponent>(puff.Entity, out var sprite))
            {
                _puffs.RemoveAt(i);
                continue;
            }
            var seconds = (float) (now - puff.Start).TotalSeconds;
            var offset = puff.Drift * seconds - Vector2.UnitY * (puff.Gravity * seconds * seconds / 2);
            var snapped = SnapToPixel(offset);
            if (sprite.Offset != snapped)
                _sprites.SetOffset((puff.Entity, sprite), snapped);
        }

        for (var i = _shards.Count - 1; i >= 0; i--)
        {
            var shard = _shards[i];
            var elapsed = (float) (now - shard.Start).TotalSeconds;
            var life = shard.Returning ? FishSupermatterParticleEvent.ChargeSeconds : 2.1f;
            if (elapsed >= life || !TryComp<SpriteComponent>(shard.Entity, out var sprite))
            {
                TryQueueDel(shard.Entity);
                _shards.RemoveAt(i);
                continue;
            }

            var fade = shard.Returning ? 1f : Math.Clamp((life - elapsed) / 0.65f, 0f, 1f);
            var tint = Color.White.WithAlpha(fade);
            if (sprite.Color != tint)
                _sprites.SetColor((shard.Entity, sprite), tint);
            var offset = SnapToPixel(ShardPosition(shard, elapsed));
            if (sprite.Offset != offset)
                _sprites.SetOffset((shard.Entity, sprite), offset);
        }
    }

    private void EmitBorgSmoke(TimeSpan now)
    {
        var query = EntityQueryEnumerator<BorgChassisComponent, DamageableComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var damage, out var xform, out var meta))
        {
            if ((meta.Flags & MetaDataFlags.Detached) != 0)
                continue;

            var total = _damage.GetTotalDamage((uid, damage));
            if (total <= 0)
            {
                _nextPuff.Remove(uid);
                continue;
            }

            if (_puffs.Count >= MaxSmoke || _nextPuff.ContainsKey(uid) ||
                xform.MapUid == null || _containers.IsEntityOrParentInContainer(uid) ||
                !_thresholds.TryGetIncapPercentage(uid, total, out var percentage))
                continue;

            var severity = Math.Clamp(percentage.Value.Float(), 0f, 1f);
            _nextPuff[uid] = now + TimeSpan.FromSeconds((1.08f - 0.87f * severity) * _random.NextFloat(0.9f, 1.1f));
            // Точка выпуска привязана к полу. Борг продолжает движение, а старый дым остаётся позади.
            var floor = _transform.ToCoordinates(xform.GridUid ?? xform.MapUid.Value, _transform.GetMapCoordinates(xform));
            var clouds = severity >= 0.8f ? 2 : 1;
            for (var i = 0; i < clouds && _puffs.Count < MaxSmoke; i++)
            {
                var vent = new Vector2(_random.NextFloat(-0.18f, 0.18f), _random.NextFloat(0.09f, 0.22f));
                var puff = SpawnAttachedTo(Smoke, floor.Offset(vent));
                var scale = _random.NextFloat(0.85f, 1.08f) + 0.45f * severity;
                _sprites.SetScale(puff, new Vector2(scale));
                _sprites.SetColor(puff, Color.InterpolateBetween(LightSmoke, HeavySmoke, severity));
                var drift = new Vector2(_random.NextFloat(-10, 10), _random.NextFloat(15, 23) + 10 * severity);
                _puffs.Add(new Puff(puff, drift, now));
            }

            if (severity >= 1f && _puffs.Count <= MaxSmoke - 2 && _random.Prob(0.14f))
            {
                var side = _random.Prob(0.5f) ? 1f : -1f;
                var contact = floor.Offset(new Vector2(side * 0.21f, 0.12f));
                for (var i = 0; i < 2; i++)
                {
                    var spark = SpawnAttachedTo(Spark, contact);
                    var drift = new Vector2(side * _random.NextFloat(10, 29), _random.NextFloat(15, 38));
                    _puffs.Add(new Puff(spark, drift, now, 130f));
                }
            }
        }
    }

    private void OnCrystalEffect(FishSupermatterParticleEvent ev)
    {
        if (!_enabled || !TryGetEntity(ev.Crystal, out var entity))
            return;

        var crystal = entity.Value;
        var now = _timing.CurTime;
        var returning = ev.Effect == FishSupermatterParticleEffect.Charge;
        var pulled = 0;
        if (returning)
        {
            for (var i = 0; i < _shards.Count; i++)
            {
                var shard = _shards[i];
                if (shard.Crystal != crystal || !Exists(shard.Entity))
                    continue;
                var age = (float) (now - shard.Start).TotalSeconds;
                var offset = ShardPosition(shard, age);
                // При сколе и зарядке за один тик сначала даём фрагменту оторваться от яркой грани.
                var turn = !shard.Returning && age < 0.10f ? shard.To : offset;
                _shards[i] = shard with { Start = now, From = offset, To = turn, Returning = true };
                if (TryComp<TimedDespawnComponent>(shard.Entity, out var despawn))
                    despawn.Lifetime = MathF.Max(despawn.Lifetime, FishSupermatterParticleEvent.ChargeSeconds);
                pulled++;
            }
        }

        // У наблюдателя, который только вошёл в PVS, могут ещё отсутствовать отделившиеся осколки.
        var count = returning ? Math.Max(0, 5 - pulled) : 4;
        var origin = new EntityCoordinates(crystal, Vector2.Zero);
        for (var i = 0; i < count && _shards.Count < MaxChips; i++)
        {
            var angle = _random.NextFloat(0f, MathF.Tau);
            var radial = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var edge = radial * new Vector2(11, 17);
            var outside = edge + radial * _random.NextFloat(10, 17);
            var particle = SpawnAttachedTo(Chip, origin);
            var from = returning ? outside : edge;
            _sprites.SetOffset(particle, SnapToPixel(from));
            _shards.Add(new Shard(particle, crystal, from, outside, now, returning));
        }
    }

    private static Vector2 ShardPosition(Shard shard, float seconds)
    {
        if (shard.Returning)
        {
            var t = Math.Clamp(seconds / FishSupermatterParticleEvent.ChargeSeconds, 0f, 1f);
            if (shard.From != shard.To)
            {
                const float turn = 0.33f;
                if (t < turn)
                    return Vector2.Lerp(shard.From, shard.To, t / turn);
                t = (t - turn) / (1f - turn);
            }
            return Vector2.Lerp(shard.To, Vector2.Zero, t * t);
        }

        var remaining = Math.Clamp(1f - seconds / 0.55f, 0f, 1f);
        return Vector2.Lerp(shard.To, shard.From, remaining * remaining);
    }

    private static Vector2 SnapToPixel(Vector2 pixels) => new(MathF.Round(pixels.X) / 32, MathF.Round(pixels.Y) / 32);
}
