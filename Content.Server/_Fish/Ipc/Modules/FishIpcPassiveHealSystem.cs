using Content.Shared._Fish.Ipc;
using Content.Shared._Fish.Ipc.Modules;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Fish.Ipc.Modules;

/// <summary>
/// Пассивный хил установленных модулей (наноремонт / реактивный).
/// Поддерживает и новый <see cref="FishIpcPassiveHealModuleComponent"/>, и legacy NanoRepair.
/// </summary>
public sealed partial class FishIpcPassiveHealSystem : EntitySystem
{
    [Dependency] private FishIpcModuleSystem _modules = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        TickHeal<FishIpcPassiveHealModuleComponent>(frameTime, static (_, heal, _) => heal.HealPerSecond);
        TickHeal<FishIpcNanoRepairModuleComponent>(frameTime, static (_, heal, _) => heal.HealPerSecond);
    }

    private void TickHeal<T>(float frameTime, Func<EntityUid, T, FishIpcModuleComponent, Content.Shared.Damage.DamageSpecifier> getHeal)
        where T : Component
    {
        var query = EntityQueryEnumerator<T, FishIpcModuleComponent>();
        while (query.MoveNext(out var uid, out var healComp, out var module))
        {
            if (!module.Installed || module.InstalledEntity is not { } host)
                continue;

            if (TerminatingOrDeleted(host) || _mobState.IsDead(host))
                continue;

            if (TryComp<FishIpcComponent>(host, out var ipc) && ipc.PowerDown)
                continue;

            var fraction = _modules.GetHostChargeFraction(host);
            if (fraction < module.MinChargeFraction)
                continue;

            var heal = getHeal(uid, healComp, module) * frameTime;
            _damageable.TryChangeDamage(host, heal, ignoreResistances: true, interruptsDoAfters: false);
        }
    }
}
