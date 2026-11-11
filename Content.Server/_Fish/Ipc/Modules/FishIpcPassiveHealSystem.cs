using Content.Shared._Fish.Ipc.Modules;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Fish.Ipc.Modules;

/// <summary>
/// Пассивный хил установленных модулей (наноремонт / реактивный).
/// Накапливает дробный heal, иначе FixedPoint2 округляет крохи за кадр в ноль.
/// </summary>
public sealed partial class FishIpcPassiveHealSystem : EntitySystem
{
    [Dependency] private FishIpcModuleSystem _modules = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        TickHeal(frameTime);
    }

    private void TickHeal(float frameTime)
    {
        var query = EntityQueryEnumerator<FishIpcPassiveHealModuleComponent, FishIpcModuleComponent>();
        while (query.MoveNext(out _, out var healComp, out var module))
        {
            if (!module.Installed || module.InstalledEntity is not { } host)
                continue;

            if (!healComp.Enabled)
                continue;

            if (TerminatingOrDeleted(host) || _mobState.IsDead(host))
                continue;

            if (!_modules.IsChassisPowered(host))
                continue;

            if (_damageable.GetTotalDamage(host) <= 0)
                continue;

            var fraction = _modules.GetHostChargeFraction(host);
            if (fraction < module.MinChargeFraction)
                continue;

            Accumulate(healComp, frameTime);
            healComp.ApplyTimer += frameTime;
            if (healComp.ApplyTimer < 1f)
                continue;

            healComp.ApplyTimer -= MathF.Floor(healComp.ApplyTimer);
            Flush(healComp, host);
        }
    }

    private static void Accumulate(FishIpcPassiveHealModuleComponent healComp, float frameTime)
    {
        foreach (var (key, perSecond) in healComp.HealPerSecond.DamageDict)
        {
            healComp.Accumulator.TryGetValue(key, out var acc);
            healComp.Accumulator[key] = acc + perSecond.Float() * frameTime;
        }
    }

    private void Flush(FishIpcPassiveHealModuleComponent healComp, EntityUid host)
    {
        var toApply = new DamageSpecifier();

        foreach (var (key, acc) in healComp.Accumulator)
        {
            var cents = (int) MathF.Truncate(acc * 100f);
            if (cents == 0)
                continue;

            var apply = cents / 100f;
            healComp.Accumulator[key] = acc - apply;
            toApply.DamageDict[key] = FixedPoint2.New(apply);
        }

        if (toApply.Empty)
            return;

        // Разброс урона (до −30%) съедает порцию 0.01. Ремонт — ровный поток раз в секунду.
        _damageable.TryChangeDamage(
            host,
            toApply,
            ignoreResistances: true,
            interruptsDoAfters: false,
            ignoreVariance: true);
    }
}
