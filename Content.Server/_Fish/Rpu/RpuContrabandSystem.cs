using Content.Shared.Contraband;
using Content.Shared.Storage.Components;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Контрабанда: следим, сколько времени запрещённые предметы лежат в инвентаре.
/// Опрос раз в секунду (движковый <c>ContainerHasContraband</c> сам рекурсивно обходит
/// контейнеры), пороги задаются конфигами: количество и минуты ношения.
/// </summary>
public sealed partial class RpuContrabandSystem : EntitySystem
{
    [Dependency] private RpuSystem _rpu = default!;
    [Dependency] private ContrabandSystem _contraband = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;

    private const double CheckInterval = 1.0;

    private TimeSpan _nextCheck;

    /// <summary>Когда в последний раз держим в инвентаре контрабанду.</summary>
    private readonly Dictionary<NetUserId, TimeSpan> _since = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartedEvent>(_ => _since.Clear());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _since.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + TimeSpan.FromSeconds(CheckInterval);

        if (!_rpu.Active)
            return;

        var limit = _rpu.ContrabandLimit;
        var dwell = TimeSpan.FromMinutes(_rpu.ContrabandDwellMinutes);

        foreach (var session in _players.Sessions)
        {
            if (session.Status != SessionStatus.InGame || session.AttachedEntity is not { Valid: true } attached)
            {
                _since.Remove(session.UserId);
                continue;
            }

            if (!_rpu.PunishmentAllowed(session))
            {
                _since.Remove(session.UserId);
                continue;
            }

            if (!_contraband.ContainerHasContraband(attached, attached, out var found) || found.Count < limit)
            {
                _since.Remove(session.UserId);
                continue;
            }

            if (!_since.TryGetValue(session.UserId, out var since))
            {
                _since[session.UserId] = _timing.CurTime;
                continue;
            }

            if (_timing.CurTime - since < dwell)
                continue;

            // Считаем один раз за эпизод, затем обнуляем таймер: дальше снова 12 минут ношения.
            _since[session.UserId] = _timing.CurTime;
            _rpu.Penalize(session, RpuPrototypeIds.ContrabandItem,
                details: $"carrying {found.Count} contraband groups");
        }
    }
}
