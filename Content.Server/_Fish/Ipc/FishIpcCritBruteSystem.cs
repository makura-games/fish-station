using Content.Shared._Fish.Ipc;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Fish.Ipc;

/// <summary>В crit КПБ получают continuous brute (Paradise: нет oxy → brute).</summary>
public sealed partial class FishIpcCritBruteSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private static readonly DamageSpecifier CritDamage = new()
    {
        DamageDict = { ["Blunt"] = 0.4f }
    };

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<FishIpcComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState))
        {
            if (TerminatingOrDeleted(uid) || !_mobState.IsCritical(uid, mobState))
                continue;

            _damageable.TryChangeDamage(uid, CritDamage * frameTime, true, false);
        }
    }
}
