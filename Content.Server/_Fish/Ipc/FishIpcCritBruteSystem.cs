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

    /// <summary>Базовый blunt/с при crit. В урон уходит целыми единицами, без спама 0.01.</summary>
    private const float CritBrutePerSecond = 0.4f;

    private const float CritChunk = 1f;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<FishIpcComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var ipc, out var mobState))
        {
            if (TerminatingOrDeleted(uid) || !_mobState.IsCritical(uid, mobState))
            {
                ipc.CritBruteRemainder = 0f;
                continue;
            }

            // 0.4/с копятся и уходят пачками по 1, иначе каждый кадр всплывает −0.01.
            ipc.CritBruteRemainder += CritBrutePerSecond * frameTime;
            if (ipc.CritBruteRemainder < CritChunk)
                continue;

            var chunks = MathF.Floor(ipc.CritBruteRemainder / CritChunk);
            var apply = chunks * CritChunk;
            ipc.CritBruteRemainder -= apply;
            var damage = new DamageSpecifier
            {
                DamageDict = { ["Blunt"] = apply }
            };
            // Порция 0.01 без ignoreVariance обнуляется разбросом урона (до −30%).
            // Жужжание от удара эту пачку не считает: иначе каждый целый ушиб в крите пищит.
            ipc.SuppressDamageBuzz = true;
            try
            {
                _damageable.TryChangeDamage(uid, damage, ignoreResistances: true, interruptsDoAfters: false, ignoreVariance: true);
            }
            finally
            {
                ipc.SuppressDamageBuzz = false;
            }
        }
    }
}
