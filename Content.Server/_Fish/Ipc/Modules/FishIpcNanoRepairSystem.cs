using Content.Shared._Fish.Ipc.Modules;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Fish.Ipc.Modules;

/// <summary>
/// Пассивный наноремонт установленных модулей КПБ.
/// </summary>
public sealed partial class FishIpcNanoRepairSystem : EntitySystem
{
    [Dependency] private FishIpcModuleSystem _modules = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FishIpcNanoRepairModuleComponent, FishIpcModuleComponent>();
        while (query.MoveNext(out var uid, out var repair, out var module))
        {
            if (!module.Installed || module.InstalledEntity is not { } host)
                continue;

            if (TerminatingOrDeleted(host) || _mobState.IsDead(host))
                continue;

            var fraction = _modules.GetHostChargeFraction(host);
            if (fraction < module.MinChargeFraction)
                continue;

            var heal = repair.HealPerSecond * frameTime;
            _damageable.TryChangeDamage(host, heal, ignoreResistances: true, interruptsDoAfters: false);
        }
    }
}
