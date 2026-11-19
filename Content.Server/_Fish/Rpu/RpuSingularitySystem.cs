using Content.Server.ParticleAccelerator.Components;
using Content.Server.ParticleAccelerator.EntitySystems;
using Content.Server.Singularity.Components;
using Content.Server.Tesla.Components;
using Content.Shared._Fish.Rpu;
using Content.Shared.Singularity.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Сингулярность и тесла без присмотра. Напоминаем о себе только тем, кто реально
/// участвовал в сборке: коллекторы, эмиттеры, ускоритель, генераторы поля и сингулярности,
/// катушки теслы, заземляющие стержни.
/// </summary>
public sealed partial class RpuSingularitySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RpuSystem _rpu = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPlayerManager _players = default!;

    private readonly HashSet<ICommonSession> _caretakers = new();

    private EntityCoordinates? _well;
    private TimeSpan _unattendedSince;
    private TimeSpan _nextCheck;
    private bool _escapedCharged;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadiationCollectorComponent, InteractUsingEvent>(OnTouched);
        SubscribeLocalEvent<ParticleAcceleratorControlBoxComponent, InteractUsingEvent>(OnTouched);
        SubscribeLocalEvent<EmitterComponent, InteractUsingEvent>(OnTouched);
        SubscribeLocalEvent<ContainmentFieldGeneratorComponent, InteractUsingEvent>(OnTouched);
        SubscribeLocalEvent<SingularityGeneratorComponent, InteractUsingEvent>(OnTouched);
        SubscribeLocalEvent<TeslaCoilComponent, InteractUsingEvent>(OnTouched);
        SubscribeLocalEvent<SingularityComponent, ComponentStartup>(OnSingularitySpawned);
        SubscribeLocalEvent<SingularityComponent, ComponentRemove>(OnSingularityGone);
    }

    private void OnTouched(EntityUid uid, Component component, ref InteractUsingEvent args)
    {
        if (args.Handled || !_rpu.Active)
            return;

        if (CompOrNull<ActorComponent>(args.User)?.PlayerSession is { } session)
            _caretakers.Add(session);
    }

    private void OnSingularitySpawned(EntityUid uid, SingularityComponent component, ComponentStartup args)
    {
        _well = _transform.GetMoverCoordinates(uid);
        _unattendedSince = _timing.CurTime;
        _escapedCharged = false;
    }

    private void OnSingularityGone(EntityUid uid, SingularityComponent component, ComponentRemove args)
    {
        if (_well == null)
            return;

        _well = null;
        _caretakers.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_well == null || !_rpu.Active)
            return;

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + TimeSpan.FromSeconds(15);

        var center = _transform.ToMapCoordinates(_well.Value);
        var watchTiles = _rpu.SingularityWatchTiles;
        var watched = false;

        foreach (var session in _players.Sessions)
        {
            if (session.Status != SessionStatus.InGame || session.AttachedEntity is not { } mob)
                continue;

            var position = _transform.GetMapCoordinates(mob);
            if (position.MapId != center.MapId)
                continue;

            if ((position.Position - center.Position).Length() > watchTiles)
                continue;

            watched = true;
            break;
        }

        if (watched)
        {
            _unattendedSince = _timing.CurTime;
            return;
        }

        // Побег: гравитационный колодец ушёл далеко от точки сборки.
        var now = _timing.CurTime;
        var currentWell = FindWellPosition();

        if (currentWell != null && (currentWell.Value.Position - center.Position).Length() > watchTiles * 0.5f)
        {
            if (!_escapedCharged)
            {
                _escapedCharged = true;
                Charge(RpuPrototypeIds.SingularityEscaped, "singularity or tesla escaped");
            }

            return;
        }

        if (now - _unattendedSince < TimeSpan.FromMinutes(_rpu.SingularityWatchMinutes))
            return;

        Charge(RpuPrototypeIds.SingularityUnattended, "singularity left unattended");
        _unattendedSince = now;
    }

    private MapCoordinates? FindWellPosition()
    {
        var query = EntityQueryEnumerator<SingularityComponent>();
        while (query.MoveNext(out var uid, out _))
            return _transform.GetMapCoordinates(uid);

        var tesla = EntityQueryEnumerator<TeslaEnergyBallComponent>();
        while (tesla.MoveNext(out var uid, out _))
            return _transform.GetMapCoordinates(uid);

        return null;
    }

    private void Charge(ProtoId<RpuViolationPrototype> violation, string details)
    {
        foreach (var session in _caretakers.ToArray())
            _rpu.Penalize(session, violation, details: details);
    }
}
