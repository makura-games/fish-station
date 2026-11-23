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
using Content.Shared.Ghost.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
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
    [Dependency] private MobStateSystem _mobState = default!;

    private EntityQuery<GhostComponent> _ghostQuery;

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

        /// <summary>Оборудование именно этого колодца -> игроки и время касания.</summary>
        public readonly Dictionary<EntityUid, Dictionary<NetUserId, TimeSpan>> EquipmentCaretakers = new();
    }

    private readonly Dictionary<EntityUid, WellState> _wells = new();

    /// <summary>Игроки, касавшиеся конкретной единицы оборудования до привязки к колодцу.</summary>
    private readonly Dictionary<EntityUid, Dictionary<NetUserId, TimeSpan>> _equipmentCaretakers = new();

    /// <summary>Радиус связи оборудования установки с её колодцем.</summary>
    private const float EquipmentLinkRange = 20f;

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadiationCollectorComponent, InteractUsingEvent>((ent, ref args) => Touched(ent.Owner, ref args));
        SubscribeLocalEvent<ParticleAcceleratorControlBoxComponent, InteractUsingEvent>((ent, ref args) => Touched(ent.Owner, ref args));
        SubscribeLocalEvent<EmitterComponent, InteractUsingEvent>((ent, ref args) => Touched(ent.Owner, ref args));
        SubscribeLocalEvent<ContainmentFieldGeneratorComponent, InteractUsingEvent>((ent, ref args) => Touched(ent.Owner, ref args));
        SubscribeLocalEvent<SingularityGeneratorComponent, InteractUsingEvent>((ent, ref args) => Touched(ent.Owner, ref args));
        SubscribeLocalEvent<TeslaCoilComponent, InteractUsingEvent>((ent, ref args) => Touched(ent.Owner, ref args));

        // ComponentStartup на этих сущностях уже занят движком, поэтому колодцы находим
        // опросом в Update, а удаление слушаем явно: чистим только состояние ушедшего колодца.
        SubscribeLocalEvent<SingularityComponent, ComponentRemove>(OnWellGone);
        SubscribeLocalEvent<TeslaEnergyBallComponent, ComponentRemove>(OnWellGone);

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);

        _ghostQuery = GetEntityQuery<GhostComponent>();
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        _wells.Clear();
        _equipmentCaretakers.Clear();
    }

    private void Touched(EntityUid equipment, ref InteractUsingEvent args)
    {
        if (args.Handled || !_rpu.Active)
            return;

        if (CompOrNull<ActorComponent>(args.User)?.PlayerSession is not { } session)
            return;

        var now = _timing.CurTime;

        // Если оборудование уже привязано, сохраняем его связь с этой установкой:
        // повторное касание не должно переносить историю к соседнему колодцу.
        WellState? well = null;
        foreach (var state in _wells.Values)
        {
            if (!state.EquipmentCaretakers.ContainsKey(equipment))
                continue;

            well = state;
            break;
        }

        well ??= FindNearestWell(equipment);

        // Уже обнаруженная установка получает касание в собственный словарь, не теряя
        // прежние записи других игроков. Если касание было отложено до обнаружения,
        // объединяем его с историей установки, сохраняя последнее время каждого игрока.
        if (well != null)
        {
            if (!well.EquipmentCaretakers.TryGetValue(equipment, out var linkedCaretakers))
            {
                linkedCaretakers = new Dictionary<NetUserId, TimeSpan>();
                well.EquipmentCaretakers[equipment] = linkedCaretakers;
            }

            if (_equipmentCaretakers.Remove(equipment, out var pending))
            {
                foreach (var (userId, time) in pending)
                {
                    if (!linkedCaretakers.TryGetValue(userId, out var previous) || time > previous)
                        linkedCaretakers[userId] = time;
                }
            }

            linkedCaretakers[session.UserId] = now;
        }
        else
        {
            // Установка ещё не обнаружена — сохраняем ожидание отдельно от истории
            // уже связанных колодцев.
            if (!_equipmentCaretakers.TryGetValue(equipment, out var pending))
            {
                pending = new Dictionary<NetUserId, TimeSpan>();
                _equipmentCaretakers[equipment] = pending;
            }

            pending[session.UserId] = now;
        }
    }

    private void OnWellGone<T>(Entity<T> ent, ref ComponentRemove args) where T : IComponent
    {
        // Удалённая установка не передаёт свою историю касаний соседней установке.
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
            _equipmentCaretakers.Clear();
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
        // Сначала регистрируем все колодцы, потом связываем оборудование. Если между
        // опросами появились два колодца, привязка после первого не должна забирать
        // ожидающие касания до того, как в выбор ближайшего попадёт второй.
        var linkedAny = false;

        var query = EntityQueryEnumerator<SingularityComponent>();
        while (query.MoveNext(out var uid, out _))
            linkedAny |= Track(uid);

        var tesla = EntityQueryEnumerator<TeslaEnergyBallComponent>();
        while (tesla.MoveNext(out var uid, out _))
            linkedAny |= Track(uid);

        if (linkedAny)
            LinkNearbyEquipment();
    }

    /// <returns>True, если колодец был обнаружен впервые.</returns>
    private bool Track(EntityUid uid)
    {
        if (_wells.ContainsKey(uid))
            return false;

        _wells[uid] = new WellState
        {
            Spawn = _transform.GetMoverCoordinates(uid),
            UnattendedSince = _timing.CurTime,
        };

        return true;
    }

    /// <summary>
    /// Каждая деталь связана только с ближайшим колодцем на той же карте. Связь хранится
    /// на конкретном колодце, чтобы касание оборудования B не отвечало за установку A.
    /// </summary>
    private void LinkNearbyEquipment()
    {
        var stale = new List<EntityUid>();

        foreach (var (equipment, caretakers) in _equipmentCaretakers)
        {
            if (Deleted(equipment))
            {
                stale.Add(equipment);
                continue;
            }

            var target = FindNearestWell(equipment);
            if (target == null)
                continue;

            // Переносим только касания, сделанные до появления какой-либо установки.
            // Уже привязанное оборудование из этого словаря удалено, и его история
            // никогда не мигрирует к следующему колодцу.
            target.EquipmentCaretakers[equipment] = caretakers;
            stale.Add(equipment);
        }

        foreach (var equipment in stale)
            _equipmentCaretakers.Remove(equipment);
    }

    /// <summary>Ближайший колодец на той же карте в радиусе связи с оборудованием.</summary>
    private WellState? FindNearestWell(EntityUid equipment)
    {
        if (Deleted(equipment))
            return null;

        var equipmentCoords = _transform.GetMapCoordinates(equipment);
        WellState? nearest = null;
        var nearestDistanceSquared = EquipmentLinkRange * EquipmentLinkRange;

        foreach (var (uid, state) in _wells)
        {
            if (Deleted(uid))
                continue;

            var spawn = _transform.ToMapCoordinates(state.Spawn);
            if (spawn.MapId != equipmentCoords.MapId)
                continue;

            var distanceSquared = (spawn.Position - equipmentCoords.Position).LengthSquared();
            if (distanceSquared >= nearestDistanceSquared)
                continue;

            nearest = state;
            nearestDistanceSquared = distanceSquared;
        }

        return nearest;
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

            // Призрак и другой наблюдатель не обслуживают установку: их присутствие
            // не должно откладывать нарушение «оставлена без присмотра».
            if (_ghostQuery.HasComp(mob) || !_mobState.IsAlive(mob))
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

        foreach (var (equipment, caretakers) in _equipmentCaretakers.ToArray())
        {
            if (Deleted(equipment))
            {
                _equipmentCaretakers.Remove(equipment);
                continue;
            }

            foreach (var (userId, time) in caretakers.ToArray())
            {
                if (time >= cutoff)
                    continue;

                caretakers.Remove(userId);
            }

            if (caretakers.Count == 0)
                _equipmentCaretakers.Remove(equipment);
        }

        foreach (var state in _wells.Values)
        {
            foreach (var (equipment, caretakers) in state.EquipmentCaretakers.ToArray())
            {
                foreach (var (userId, time) in caretakers.ToArray())
                {
                    if (time >= cutoff)
                        continue;

                    caretakers.Remove(userId);
                }

                // Существование оборудования не влияет на срок ответственности: его могли
                // поглотить или удалить, но касание продолжает действовать до CaretakerMemory.
                if (caretakers.Count == 0)
                    state.EquipmentCaretakers.Remove(equipment);
            }
        }
    }

    private void Charge(WellState state, ProtoId<RpuViolationPrototype> violation, string details)
    {
        var now = _timing.CurTime;
        var responsible = new HashSet<NetUserId>();

        foreach (var caretakers in state.EquipmentCaretakers.Values)
        {
            foreach (var (userId, time) in caretakers)
            {
                if (now - time <= CaretakerMemory)
                    responsible.Add(userId);
            }
        }

        foreach (var userId in responsible)
        {
            // Игрок мог выйти и вернуться: ищем его текущую сессию по идентификатору.
            if (!_rpu.TryGetSession(userId, out var session) || session.Status != SessionStatus.InGame)
                continue;

            _rpu.Penalize(session, violation, details: details);
        }
    }

}
