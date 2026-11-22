using Content.Shared._Fish.Rpu;
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
using Robust.Shared.Player;
using Robust.Shared.Random;
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

    private EntityQuery<ActorComponent> _actorQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();

        SubscribeLocalEvent<RoundStartedEvent>(_ => _lastHit.Clear());
    }

    /// <summary>
    /// Запоминаем удар. Репарцуем только пары «игрок -> игрок»: ответный огонь оценивается
    /// по жертве-игроку, а учёт стен, ящиков и ботов раздувал бы словарь до сброса целиком.
    /// </summary>
    public void RecordDamage(EntityUid victim, EntityUid? origin, FixedPoint2 total)
    {
        if (total <= 0 || origin is not { } attacker)
            return;

        if (attacker == victim)
            return;

        if (!_actorQuery.HasComp(victim) || !_actorQuery.HasComp(attacker))
            return;

        Prune();
        _lastHit[(attacker, victim)] = _timing.CurTime;
    }

    /// <summary>
    /// Наносил ли атаковавший урон жертве в последние две минуты.
    /// </summary>
    public bool AttackedRecently(EntityUid? attacker, EntityUid? victim)
    {
        if (attacker == null || victim == null)
            return false;

        var cutoff = _timing.CurTime - AggressionWindow;

        return _lastHit.TryGetValue((attacker.Value, victim.Value), out var time)
               && time >= cutoff;
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

        List<(EntityUid, EntityUid)>? expired = null;

        foreach (var (key, time) in _lastHit)
        {
            if (time >= cutoff)
                continue;

            expired ??= new List<(EntityUid, EntityUid)>();
            expired.Add(key);
        }

        if (expired != null)
        {
            foreach (var key in expired)
                _lastHit.Remove(key);
        }

        if (_lastHit.Count <= MemoryEntries)
            return;

        // Совсем редкий случай (толпа на маленькой карте): сбрасываем самые старые пары,
        // а не всю память — недавние нападения должны остаться видимыми.
        var oldest = _lastHit
            .OrderBy(pair => pair.Value)
            .Take(_lastHit.Count - MemoryEntries)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in oldest)
            _lastHit.Remove(key);
    }
}
