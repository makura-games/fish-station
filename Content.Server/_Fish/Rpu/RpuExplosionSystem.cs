using Content.Shared._Fish.Rpu;
using Content.Shared._Sunrise.Storyteller;
using Content.Shared.Explosion.Components;
using Content.Shared.Trigger;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Взрывы. Движок не отдаёт источник взрыва в событии, поэтому система сама помнит,
/// кто ставил/бросал взрывчатку, и сопоставляет это с фактом взрыва.
/// </summary>
public sealed partial class RpuExplosionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RpuSystem _rpu = default!;
    [Dependency] private SharedTransformSystem _transforms = default!;
    [Dependency] private IPlayerManager _players = default!;

    /// <summary>Интенсивность и размер взрыва по тоннам прототипов: до 6 — мелочь, до 25 — средний, дальше крупный.</summary>
    public const float SmallIntensity = 6f;
    public const float MediumIntensity = 25f;

    private static readonly TimeSpan MemoryWindow = TimeSpan.FromSeconds(30);
    private static readonly float NearbyRadius = 12f;

    private readonly List<Priming> _primings = new();

    private readonly record struct Priming(EntityUid Entity, NetUserId User, EntityCoordinates Position, TimeSpan Time);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SunriseExplosionEvent>(OnExplosion);
        SubscribeLocalEvent<ExplosiveComponent, TriggerEvent>(OnTrigger);
        SubscribeLocalEvent<ExplosiveComponent, ActiveTimerTriggerEvent>(OnTimerStarted);
        SubscribeLocalEvent<ExplosiveComponent, LandEvent>(OnLand);
    }

    private void OnTrigger(EntityUid uid, ExplosiveComponent component, TriggerEvent args)
    {
        if (args.User is { } user)
            Remember(uid, user);
    }

    private void OnTimerStarted(EntityUid uid, ExplosiveComponent component, ActiveTimerTriggerEvent args)
    {
        if (args.User is { } user)
            Remember(uid, user);
    }

    private void OnLand(EntityUid uid, ExplosiveComponent component, LandEvent args)
    {
        // Снаряд приземлился: обновляем позицию, чтобы сопоставить её с эпицентром взрыва.
        for (var index = 0; index < _primings.Count; index++)
        {
            if (_primings[index].Entity != uid)
                continue;

            _primings[index] = _primings[index] with { Position = Transform(uid).Coordinates };
            return;
        }
    }

    private void Remember(EntityUid uid, EntityUid userEntity)
    {
        if (!_rpu.Active)
            return;

        if (CompOrNull<ActorComponent>(userEntity)?.PlayerSession is not { } session)
            return;

        _primings.Add(new Priming(uid, session.UserId, Transform(uid).Coordinates, _timing.CurTime));
        Prune();
    }

    private void Prune()
    {
        var cutoff = _timing.CurTime - MemoryWindow;
        _primings.RemoveAll(priming => priming.Time < cutoff);
    }

    private void OnExplosion(SunriseExplosionEvent args)
    {
        if (!_rpu.Active)
            return;

        var violation = args.Intensity switch
        {
            < SmallIntensity => RpuPrototypeIds.ExplosionSmall,
            < MediumIntensity => RpuPrototypeIds.ExplosionMedium,
            _ => RpuPrototypeIds.ExplosionLarge,
        };

        var severity = 1f;
        if (args.Intensity >= SmallIntensity)
            severity = Math.Clamp(args.AffectedTiles / 10f, 1f, 2.5f);

        var cutoff = _timing.CurTime - MemoryWindow;
        var charged = false;

        for (var index = _primings.Count - 1; index >= 0; index--)
        {
            var priming = _primings[index];

            if (priming.Time < cutoff)
            {
                _primings.RemoveAt(index);
                continue;
            }

            // Сопоставляем взрыв с тем, кто поставил эту взрывчатку: либо она уже исчезла
            // (взрыв удаляет предмет), либо она рядом с эпицентром.
            var gone = Deleted(priming.Entity);
            var distance = (_transforms.ToMapCoordinates(priming.Position).Position - args.Epicenter.Position).Length();

            if (!gone && distance > NearbyRadius)
                continue;

            if (!_rpu.TryGetSession(priming.User, out var session) || session.Status != SessionStatus.InGame)
                continue;

            _rpu.Penalize(session, violation, severity: severity, details: "explosion");
            _primings.RemoveAt(index);
            charged = true;
        }

        if (!charged)
            Prune();
    }
}
