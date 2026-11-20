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
/// Запись одна на предмет: повторная активация обновляет её, а не добавляет вторую.
/// </summary>
public sealed partial class RpuExplosionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RpuSystem _rpu = default!;
    [Dependency] private SharedTransformSystem _transforms = default!;

    /// <summary>Интенсивность и размер взрыва по тоннам прототипов: до 6 — мелочь, до 25 — средний, дальше крупный.</summary>
    public const float SmallIntensity = 6f;
    public const float MediumIntensity = 25f;

    private static readonly TimeSpan MemoryWindow = TimeSpan.FromSeconds(30);
    private static readonly float NearbyRadius = 12f;

    private readonly record struct Priming(NetUserId User, EntityCoordinates Position, TimeSpan Time);

    private readonly Dictionary<EntityUid, Priming> _primings = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SunriseExplosionEvent>(OnExplosion);
        SubscribeLocalEvent<ExplosiveComponent, TriggerEvent>((_, ref args) => Remember(args.User));
        SubscribeLocalEvent<ExplosiveComponent, ActiveTimerTriggerEvent>((_, ref args) => Remember(args.User));
        SubscribeLocalEvent<ExplosiveComponent, LandEvent>(OnLand);
    }

    private void Remember(EntityUid? userEntity)
    {
        if (userEntity == null || !_rpu.Active)
            return;

        if (CompOrNull<ActorComponent>(userEntity.Value)?.PlayerSession is not { } session)
            return;

        Remember(userEntity.Value, session);
    }

    private void Remember(EntityUid userEntity, ICommonSession session)
    {
        // Предмет мог быть активирован дважды (таймер и ручной запуск): держим одну запись.
        _primings[userEntity] = new Priming(session.UserId, Transform(userEntity).Coordinates, _timing.CurTime);
        Prune();
    }

    private void OnLand(Entity<ExplosiveComponent> ent, ref LandEvent args)
    {
        // Снаряд приземлился: обновляем позицию, чтобы сопоставить её с эпицентром взрыва.
        if (!_primings.TryGetValue(ent.Owner, out var priming))
            return;

        _primings[ent.Owner] = priming with { Position = Transform(ent.Owner).Coordinates };
    }

    private void Prune()
    {
        var cutoff = _timing.CurTime - MemoryWindow;
        List<EntityUid>? expired = null;

        foreach (var (uid, priming) in _primings)
        {
            if (priming.Time >= cutoff)
                continue;

            expired ??= new List<EntityUid>();
            expired.Add(uid);
        }

        if (expired == null)
            return;

        foreach (var uid in expired)
            _primings.Remove(uid);
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

        // Сверх малого заряда добавляем по очку за каждые 10 затронутых тайлов, максимум +6.
        var extra = Math.Clamp((float)Math.Floor((args.AffectedTiles - 10) / 10f), 0f, 6f);

        var cutoff = _timing.CurTime - MemoryWindow;

        // Автор — тот, чей заряд ближе всего к эпицентру на той же карте. Один взрыв
        // снимает карму один раз: соседняя чужая взрывчатка остаётся нетронутой.
        EntityUid? author = null;
        Priming authorRecord = default;
        var bestDistance = float.MaxValue;

        foreach (var (uid, priming) in _primings)
        {
            if (priming.Time < cutoff)
                continue;

            var position = _transforms.ToMapCoordinates(priming.Position);
            if (position.MapId != args.Epicenter.MapId)
                continue;

            var distance = (position.Position - args.Epicenter.Position).Length();
            if (distance > NearbyRadius || distance >= bestDistance)
                continue;

            bestDistance = distance;
            author = uid;
            authorRecord = priming;
        }

        if (author == null)
        {
            Prune();
            return;
        }

        _primings.Remove(author.Value);

        if (!_rpu.TryGetSession(authorRecord.User, out var session) || session.Status != SessionStatus.InGame)
            return;

        _rpu.Penalize(session, violation, extra: extra, details: "explosion");
    }
}
