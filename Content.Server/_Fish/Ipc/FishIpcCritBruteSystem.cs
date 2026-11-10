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

    /// <summary>Базовый blunt/с при crit (накапливается в CritBruteRemainder).</summary>
    private const float CritBrutePerSecond = 0.4f;

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

            // Копим дробный урон: при tickrate 30 * 0.4 * dt ≈ 0.013 → FixedPoint2 режет в 0.
            ipc.CritBruteRemainder += CritBrutePerSecond * frameTime;
            var apply = MathF.Floor(ipc.CritBruteRemainder * 100f) / 100f;
            if (apply <= 0f)
                continue;

            ipc.CritBruteRemainder -= apply;
            var damage = new DamageSpecifier
            {
                DamageDict = { ["Blunt"] = apply }
            };
            // Порция 0.01 без ignoreVariance обнуляется разбросом урона (до −30%).
            _damageable.TryChangeDamage(uid, damage, ignoreResistances: true, interruptsDoAfters: false, ignoreVariance: true);
        }
    }
}
