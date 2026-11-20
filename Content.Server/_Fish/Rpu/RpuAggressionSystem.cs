using Content.Shared._Fish.Rpu;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Короткая память о том, кто на кого нападал. Нужна, чтобы не наказывать за ответный огонь
/// по мелким антагонистам, которые сами начали.
/// </summary>
public sealed partial class RpuAggressionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan AggressionWindow = TimeSpan.FromSeconds(120);

    /// <summary>Не больше скольких пар держим в памяти одновременно.</summary>
    private const int MemoryEntries = 2048;

    /// <summary>Последний удар по паре «нападавший -> жертва».</summary>
    private readonly Dictionary<(EntityUid Attacker, EntityUid Victim), TimeSpan> _lastHit = new();

    private TimeSpan _nextPrune;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartedEvent>(_ => _lastHit.Clear());
    }

    /// <summary>
    /// Запоминаем удар. Сущность приводим к игроку: урон по руке или шлему того же человека
    /// считается нападением на него самого, а не на отдельный предмет.
    /// </summary>
    public void RecordDamage(EntityUid victim, EntityUid? origin, FixedPoint2 total)
    {
        if (total <= 0 || origin is not { } rawAttacker)
            return;

        var attacker = Normalize(rawAttacker);
        var target = Normalize(victim);

        if (attacker == target)
            return;

        Prune();
        _lastHit[(attacker, target)] = _timing.CurTime;
    }

    /// <summary>
    /// Наносил ли атаковавший урон жертве в последние две минуты.
    /// </summary>
    public bool AttackedRecently(EntityUid? attacker, EntityUid? victim)
    {
        if (attacker == null || victim == null)
            return false;

        var cutoff = _timing.CurTime - AggressionWindow;

        return _lastHit.TryGetValue((Normalize(attacker.Value), Normalize(victim.Value)), out var time)
               && time >= cutoff;
    }

    private EntityUid Normalize(EntityUid uid)
    {
        return CompOrNull<ActorComponent>(uid)?.Owner ?? uid;
    }

    /// <summary>
    /// Чистим просроченное не по количеству событий, а по времени: иначе пятьсот ударов
    /// по ящику стирают память о нападении два минуты назад.
    /// </summary>
    private void Prune()
    {
        if (_timing.CurTime < _nextPrune)
            return;

        _nextPrune = _timing.CurTime + TimeSpan.FromSeconds(10);

        var cutoff = _timing.CurTime - AggressionWindow;

        if (_lastHit.Count > MemoryEntries)
        {
            _lastHit.Clear();
            return;
        }

        List<(EntityUid, EntityUid)>? expired = null;

        foreach (var (key, time) in _lastHit)
        {
            if (time >= cutoff)
                continue;

            expired ??= new List<(EntityUid, EntityUid)>();
            expired.Add(key);
        }

        if (expired == null)
            return;

        foreach (var key in expired)
            _lastHit.Remove(key);
    }
}
