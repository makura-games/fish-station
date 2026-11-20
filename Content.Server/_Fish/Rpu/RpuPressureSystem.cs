using Content.Shared._Fish.Rpu;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Jittering;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// «Тихие» эффекты давления. Игроку это выглядит как невезение и лаги сети:
/// удары не регистрируются, оружие клинит, между действиями возникает микропауза,
/// на ровном месте подкашивает. Правок движка не требует.
/// </summary>
public sealed partial class RpuPressureSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private RpuSystem _rpu = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private bool _enabled;
    private float _startKarma;
    private int _latencyBase;
    private int _latencyMax;
    private float _missBase;
    private float _missMax;
    private float _slipChance;
    private float _jamChance;
    private float _dropChance;

    /// <summary>Как часто устраиваем нарушителю «невезение».</summary>
    private const float TickSeconds = 4f;

    /// <summary>На сколько секунд ноги уходят из-под ног при подкашивании.</summary>
    private static readonly TimeSpan SlipDuration = TimeSpan.FromSeconds(1.2f);

    /// <summary>Как долго трясёт после подкашивания.</summary>
    private static readonly TimeSpan JitterDuration = TimeSpan.FromSeconds(4f);

    /// <summary>Текущая глубина давления для игрока.</summary>
    public sealed class Pressure
    {
        /// <summary>Глубина давления 0..1: все тиковые эффекты умножаются на неё.</summary>
        public float Depth;
        public int LatencyMs;
        public float MissChance;
        public float SlipChance;
        public float JamChance;
        public float DropChance;
        public TimeSpan NextTick;
    }

    private readonly Dictionary<NetUserId, Pressure> _pressures = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, RpuCVars.PressureEnabled, value => _enabled = value, true);
        Subs.CVar(_cfg, RpuCVars.PressureStartKarma, value => _startKarma = value, true);
        Subs.CVar(_cfg, RpuCVars.PressureLatencyMs, value => _latencyBase = value, true);
        Subs.CVar(_cfg, RpuCVars.PressureLatencyMaxMs, value => _latencyMax = value, true);
        Subs.CVar(_cfg, RpuCVars.PressureMissChance, value => _missBase = value, true);
        Subs.CVar(_cfg, RpuCVars.PressureMissMaxChance, value => _missMax = value, true);
        Subs.CVar(_cfg, RpuCVars.PressureSlipChance, value => _slipChance = value, true);
        Subs.CVar(_cfg, RpuCVars.PressureJamChance, value => _jamChance = value, true);
        Subs.CVar(_cfg, RpuCVars.PressureDropChance, value => _dropChance = value, true);

        SubscribeLocalEvent<DamageableComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<RoundStartedEvent>(_ => _pressures.Clear());
    }

    /// <summary>
    /// Считает глубину давления по карме. Возвращает null, когда давить нельзя.
    /// Новички (< границы новичка) получают более глубокое давление.
    /// </summary>
    private Pressure? GetPressure(ICommonSession? session)
    {
        if (!_enabled || session == null || !_rpu.Active || !_rpu.PunishmentAllowed(session))
            return null;

        if (!_rpu.TryGetSuspect(session.UserId, out var suspect) || suspect.Karma >= _startKarma)
        {
            // Карма подросла (восстановление либо правка администратора) — забываем давление,
            // иначе эффекты продолжат работать с прежними вероятностями.
            _pressures.Remove(session.UserId);
            return null;
        }

        var depth = Math.Clamp((_startKarma - suspect.Karma) / Math.Abs(RpuMaths.KarmaMin), 0f, 1f);

        if (suspect.RoleHours < _rpu.NewbieBoundaryHours)
            depth = Math.Clamp(depth * 1.25f, 0f, 1f);

        if (!_pressures.TryGetValue(session.UserId, out var pressure))
        {
            pressure = new Pressure();
            _pressures[session.UserId] = pressure;
        }

        pressure.Depth = depth;
        pressure.LatencyMs = (int)Math.Round(_latencyBase + (_latencyMax - _latencyBase) * depth);
        pressure.MissChance = _missBase + (_missMax - _missBase) * depth;
        pressure.SlipChance = _slipChance * depth;
        pressure.JamChance = _jamChance * depth;
        pressure.DropChance = _dropChance * depth;

        return pressure;
    }

    private Pressure? GetPressureForEntity(EntityUid? uid)
    {
        if (uid == null)
            return null;

        return GetPressure(CompOrNull<ActorComponent>(uid.Value)?.PlayerSession);
    }

    /// <summary>
    /// Удар нарушителя может «не зарегистрироваться». Лечение под этот удар не попадает:
    /// отменять его было бы заметно, а эффект и так виден по остальным симптомам.
    /// </summary>
    private void OnBeforeDamage(Entity<DamageableComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled || args.Origin == null)
            return;

        // Нулевой или отрицательный итог — это лечение: не трогаем.
        if (args.Damage.GetTotal() <= 0)
            return;

        var attacker = GetPressureForEntity(args.Origin);

        if (attacker is { MissChance: > 0f } && _random.Prob(attacker.MissChance))
            args.Cancelled = true;
    }

    /// <summary>Осечки оружия.</summary>
    private void OnShotAttempted(ref ShotAttemptedEvent args)
    {
        var pressure = GetPressureForEntity(args.User);

        if (pressure is { JamChance: > 0f } && _random.Prob(pressure.JamChance))
            args.Cancel();
    }

    /// <summary>
    /// Редкий тик только по тем, кто уже под давлением: микропауза («лаг»),
    /// подкашивание на ровном месте, выпавший предмет. Обычных игроков не опрашиваем.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled || !_rpu.Active)
            return;

        // Глубину давления пересчитываем и для тех, кто пока никого не бил:
        // микропаузы и подкашивания должны прийти к любому нарушителю.
        foreach (var session in _players.Sessions)
        {
            if (session.Status == SessionStatus.InGame)
                GetPressure(session);
            else
                _pressures.Remove(session.UserId);
        }

        if (_pressures.Count == 0)
            return;

        var now = _timing.CurTime;

        foreach (var (userId, pressure) in _pressures)
        {
            if (now < pressure.NextTick)
                continue;

            pressure.NextTick = now + TimeSpan.FromSeconds(TickSeconds);

            if (!_rpu.TryGetSession(userId, out var session) || session.AttachedEntity is not { } mob)
                continue;

            if (!_rpu.PunishmentAllowed(session))
                continue;

            // Микропауза: длительность берём из «пинга», но в долях, чтобы не оглушать надолго.
            // Визуал оглушения выключаем: игроку это выглядит как потерянный пакет,
            // а не как «меня оглушили».
            if (pressure.LatencyMs > 0 && _random.Prob(0.25f * pressure.Depth))
            {
                var pause = TimeSpan.FromMilliseconds(pressure.LatencyMs * 2.5f);
                _stun.TryAddStunDuration(mob, pause, visualized: false);
            }

            // Подкашивание на ровном месте: ноги едут, руки трясутся, предмет валится.
            if (pressure.SlipChance > 0f && _random.Prob(pressure.SlipChance))
            {
                _stun.TryKnockdown(mob, SlipDuration, autoStand: true, drop: true);
                _jitter.DoJitter(mob, JitterDuration, refresh: true);
            }

            // Что-то вываливается из рук.
            if (pressure.DropChance > 0f && _random.Prob(pressure.DropChance))
            {
                if (TryComp<Content.Shared.Hands.Components.HandsComponent>(mob, out var handsComp) &&
                    _hands.GetActiveHand((mob, handsComp)) is { } handId)
                {
                    _hands.TryDrop((mob, handsComp), handId);
                }
            }
        }

        // Подчищаем за отключившимися.
        foreach (var userId in _pressures.Keys.ToArray())
        {
            if (!_players.SessionsDict.ContainsKey(userId))
                _pressures.Remove(userId);
        }
    }
}

