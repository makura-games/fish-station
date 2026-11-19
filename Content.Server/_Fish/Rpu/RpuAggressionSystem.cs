using Content.Shared.Damage;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Короткая память о том, кто на кого нападал. Нужна, чтобы не наказывать за ответный огонь
/// по мелким антагонистам, которые сами начали.
/// </summary>
public sealed partial class RpuAggressionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    private const int MemoryEntries = 512;
    private static readonly TimeSpan AggressionWindow = TimeSpan.FromSeconds(120);

    private readonly List<AggressionRecord> _records = new();

    private readonly record struct AggressionRecord(EntityUid Attacker, EntityUid Victim, TimeSpan Time);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageableComponent, DamageDealtEvent>(OnDamageDealt);
    }

    private void OnDamageDealt(Entity<DamageableComponent> ent, ref DamageDealtEvent args)
    {
        if (args.Damage.GetTotal() <= 0 || args.Origin is not { } origin)
            return;

        if (origin == ent.Owner)
            return;

        _records.Add(new AggressionRecord(origin, ent.Owner, _timing.CurTime));
        while (_records.Count > MemoryEntries)
            _records.RemoveAt(0);
    }

    /// <summary>
    /// Наносил ли атаковавший урон жертве (или её соседу по телу) в последние две минуты.
    /// </summary>
    public bool AttackedRecently(EntityUid? attacker, EntityUid? victim)
    {
        if (attacker == null || victim == null)
            return false;

        var cutoff = _timing.CurTime - AggressionWindow;

        for (var index = _records.Count - 1; index >= 0; index--)
        {
            var record = _records[index];
            if (record.Time < cutoff)
                break;

            if (record.Attacker == attacker.Value && record.Victim == victim.Value)
                return true;

            // Урон по мобильной части того же игрока тоже считается нападением.
            if (record.Attacker == attacker.Value && IsSamePlayer(record.Victim, victim.Value))
                return true;
        }

        return false;
    }

    private bool IsSamePlayer(EntityUid first, EntityUid second)
    {
        if (first == second)
            return true;

        var sessionFirst = CompOrNull<ActorComponent>(first)?.PlayerSession;
        var sessionSecond = CompOrNull<ActorComponent>(second)?.PlayerSession;

        return sessionFirst != null && sessionFirst == sessionSecond;
    }
}
