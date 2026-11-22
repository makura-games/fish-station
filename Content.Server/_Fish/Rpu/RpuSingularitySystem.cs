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

using System.Linq;
using Robust.Shared.Enums;
using Robust.Shared.Audio.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Gibbing;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Throwing;
using Content.Shared.Wires;
using Content.Shared.Slippery;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;
namespace Content.Server._Fish.Rpu;

/// <summary>
/// Сингулярность и тесла без присмотра. Напоминаем о себе только тем, кто реально
/// участвовал в сборке: коллекторы, эмиттеры, ускоритель, генераторы поля и сингулярности,
/// катушки теслы. За каждым колодцем свой наблюдение: поставить две сингулярности —
/// это два отдельных нарушения, а не одно на всех.
/// </summary>
public sealed partial class RpuSingularitySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RpuSystem _rpu = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPlayerManager _players = default!;

    private static readonly TimeSpan CaretakerMemory = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Состояние конкретного колодца: где его собрали, с какого момента он без присмотра,
    /// кто за него отвечает и регистрировали ли уже побег.
    /// </summary>
    private sealed class WellState
    {
        public EntityCoordinates Spawn;
        public TimeSpan UnattendedSince;
        public bool EscapedCharged;
        public readonly HashSet<ICommonSession> Caretakers = new();
    }

    private readonly Dictionary<EntityUid, WellState> _wells = new();

    /// <summary>Кто трогал оборудование содержания. Раздаётся новым колодцам при появлении.</summary>
    private readonly Dictionary<ICommonSession, TimeSpan> _caretakers = new();

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadiationCollectorComponent, InteractUsingEvent>((_, ref args) => Touched(ref args));
        SubscribeLocalEvent<ParticleAcceleratorControlBoxComponent, InteractUsingEvent>((_, ref args) => Touched(ref args));
        SubscribeLocalEvent<EmitterComponent, InteractUsingEvent>((_, ref args) => Touched(ref args));
        SubscribeLocalEvent<ContainmentFieldGeneratorComponent, InteractUsingEvent>((_, ref args) => Touched(ref args));
        SubscribeLocalEvent<SingularityGeneratorComponent, InteractUsingEvent>((_, ref args) => Touched(ref args));
        SubscribeLocalEvent<TeslaCoilComponent, InteractUsingEvent>((_, ref args) => Touched(ref args));

        // ComponentStartup на этих сущностях уже занят движком, поэтому колодцы находим
        // опросом в Update, а удаление слушаем явно: чистим только состояние ушедшего колодца.
        SubscribeLocalEvent<SingularityComponent, ComponentRemove>(OnWellGone);
        SubscribeLocalEvent<TeslaEnergyBallComponent, ComponentRemove>(OnWellGone);

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        _wells.Clear();
        _caretakers.Clear();
    }

    private void Touched(ref InteractUsingEvent args)
    {
        if (args.Handled || !_rpu.Active)
            return;

        if (CompOrNull<ActorComponent>(args.User)?.PlayerSession is { } session)
            _caretakers[session] = _timing.CurTime;
    }

    private void OnWellGone<T>(Entity<T> ent, ref ComponentRemove args) where T : IComponent
    {
        // Ушёл именно этот колодец: остальные продолжают считаться.
        _wells.Remove(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Пока счёт не ведётся, память о прикосновениях и колодцах не держим: иначе
        // после включения нарушением стало бы оборудование, к которому шли в другом состоянии.
        if (!_rpu.Active)
        {
            _wells.Clear();
            _caretakers.Clear();
            return;
        }

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + TimeSpan.FromSeconds(15);

        ForgetStaleCaretakers();
        TrackNewWells();

        if (_wells.Count == 0)
            return;

        foreach (var (uid, state) in _wells.ToArray())
        {
            if (!Exists(uid))
            {
                _wells.Remove(uid);
                continue;
            }

            CheckWell(uid, state);
        }
    }

    private void TrackNewWells()
    {
        var query = EntityQueryEnumerator<SingularityComponent>();
        while (query.MoveNext(out var uid, out _))
            Track(uid);

        var tesla = EntityQueryEnumerator<TeslaEnergyBallComponent>();
        while (tesla.MoveNext(out var uid, out _))
            Track(uid);
    }

    private void Track(EntityUid uid)
    {
        if (_wells.ContainsKey(uid))
            return;

        var state = new WellState
        {
            Spawn = _transform.GetMoverCoordinates(uid),
            UnattendedSince = _timing.CurTime,
        };

        foreach (var (session, _) in _caretakers)
            state.Caretakers.Add(session);

        _wells[uid] = state;
    }

    private void CheckWell(EntityUid uid, WellState state)
    {
        var now = _timing.CurTime;
        var spawn = _transform.ToMapCoordinates(state.Spawn);
        var current = _transform.GetMapCoordinates(uid);
        var watchTiles = _rpu.SingularityWatchTiles;

        // Побег проверяем первым: колодец мог уползти, пока «наблюдатель» стоит на старом месте.
        // Сравниваем с этим же колодцем и на той же карте.
        if (current.MapId == spawn.MapId
            && (current.Position - spawn.Position).Length() > watchTiles * 0.5f)
        {
            if (state.EscapedCharged)
                return;

            state.EscapedCharged = true;
            Charge(state, RpuPrototypeIds.SingularityEscaped, "singularity or tesla escaped");
            return;
        }

        if (IsWatched(spawn, watchTiles))
        {
            state.UnattendedSince = now;
            return;
        }

        if (now - state.UnattendedSince < TimeSpan.FromMinutes(_rpu.SingularityWatchMinutes))
            return;

        Charge(state, RpuPrototypeIds.SingularityUnattended, "singularity or tesla left unattended");
        state.UnattendedSince = now;
    }

    private bool IsWatched(MapCoordinates center, float watchTiles)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.Status != SessionStatus.InGame || session.AttachedEntity is not { } mob)
                continue;

            var position = _transform.GetMapCoordinates(mob);
            if (position.MapId != center.MapId)
                continue;

            if ((position.Position - center.Position).Length() <= watchTiles)
                return true;
        }

        return false;
    }

    private void ForgetStaleCaretakers()
    {
        var cutoff = _timing.CurTime - CaretakerMemory;

        List<ICommonSession>? stale = null;

        foreach (var (session, time) in _caretakers)
        {
            if (time >= cutoff && session.Status == SessionStatus.InGame)
                continue;

            stale ??= new List<ICommonSession>();
            stale.Add(session);
        }

        if (stale != null)
        {
            foreach (var session in stale)
                _caretakers.Remove(session);
        }

        // Множество наблюдений у каждого колодца живёт отдельно: без этой чистки
        // штраф ушёл бы игроку, который давно вышел.
        foreach (var state in _wells.Values)
        {
            foreach (var session in state.Caretakers.ToArray())
            {
                if (session.Status == SessionStatus.InGame)
                    continue;

                state.Caretakers.Remove(session);
            }
        }
    }

    private void Charge(WellState state, ProtoId<RpuViolationPrototype> violation, string details)
    {
        foreach (var session in state.Caretakers.ToArray())
        {
            if (session.Status != SessionStatus.InGame)
                continue;

            _rpu.Penalize(session, violation, details: details);
        }
    }
}
