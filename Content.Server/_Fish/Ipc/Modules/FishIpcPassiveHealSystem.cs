using Content.Shared._Fish.Ipc;
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
        while (query.MoveNext(out var uid, out var healComp, out var module))
        {
            if (!module.Installed || module.InstalledEntity is not { } host)
                continue;

            if (!healComp.Enabled)
                continue;

            if (TerminatingOrDeleted(host) || _mobState.IsDead(host))
                continue;

            if (TryComp<FishIpcComponent>(host, out var ipc) && ipc.PowerDown)
                continue;

            var fraction = _modules.GetHostChargeFraction(host);
            if (fraction < module.MinChargeFraction)
                continue;

            AccumulateAndApply(uid, healComp, host, frameTime);
        }
    }

    private void AccumulateAndApply(
        EntityUid moduleUid,
        FishIpcPassiveHealModuleComponent healComp,
        EntityUid host,
        float frameTime)
    {
        foreach (var (key, perSecond) in healComp.HealPerSecond.DamageDict)
        {
            if (!healComp.Accumulator.TryGetValue(key, out var acc))
                acc = FixedPoint2.Zero;

            acc += perSecond * frameTime;
            healComp.Accumulator[key] = acc;
        }

        var toApply = new DamageSpecifier();
        var dirty = false;

        foreach (var (key, acc) in healComp.Accumulator)
        {
            // FixedPoint2 шаг ≈ 0.01 — ждём накопления заметной порции.
            if (FixedPoint2.Abs(acc) < FixedPoint2.New(0.01))
                continue;

            toApply.DamageDict[key] = acc;
            healComp.Accumulator[key] = FixedPoint2.Zero;
            dirty = true;
        }

        if (!dirty)
            return;

        _damageable.TryChangeDamage(host, toApply, ignoreResistances: true, interruptsDoAfters: false);
        Dirty(moduleUid, healComp);
    }
}
