using Content.Shared._Fish.Ipc;
using Content.Shared._Fish.Ipc.Modules;
using Content.Shared.Mobs.Systems;
using Content.Shared.PowerCell;
using Content.Shared.Power.EntitySystems;

namespace Content.Server._Fish.Ipc.Modules;

/// <summary>
/// Сервер: пассивный расход модулей и API разового списания заряда.
/// </summary>
public sealed partial class FishIpcModuleSystem : SharedFishIpcModuleSystem
{
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FishIpcModuleHostComponent, FishIpcComponent>();
        while (query.MoveNext(out var hostUid, out var host, out var ipc))
        {
            if (TerminatingOrDeleted(hostUid) || ipc.PowerDown || _mobState.IsDead(hostUid))
                continue;

            // Контейнер мог ещё не инициализироваться на первом тике.
            if (host.ModuleContainer is not { Owner: var owner } || owner != hostUid)
                continue;

            var chargeFraction = GetHostChargeFraction(hostUid);
            var drain = 0f;
            foreach (var moduleUid in host.ModuleContainer.ContainedEntities)
            {
                if (!TryComp<FishIpcModuleComponent>(moduleUid, out var module) || !module.Installed)
                    continue;

                if (module.PassiveDrainPerSecond <= 0f)
                    continue;

                // Ниже порога пассивный расход и эффекты модулей останавливаются.
                if (chargeFraction < module.MinChargeFraction)
                    continue;

                // Выключенный toggle не должен жрать батарею.
                if (!IsModuleDrawingPower(moduleUid))
                    continue;

                drain += module.PassiveDrainPerSecond;
            }

            if (drain <= 0f)
                continue;

            _powerCell.TryUseCharge(hostUid, drain * frameTime);
        }
    }

    /// <summary>
    /// Списать заряд с батареи хоста для активного эффекта модуля.
    /// </summary>
    public bool TryConsumeModuleCharge(EntityUid host, float joules, float minChargeFraction = 0f)
    {
        if (TerminatingOrDeleted(host) || joules <= 0f)
            return false;

        if (!_powerCell.TryGetBatteryFromSlot(host, out var battery))
            return false;

        var (charge, max) = _battery.GetCharge(battery.Value.Owner);
        if (max <= 0f)
            return false;

        if (minChargeFraction > 0f && charge / max < minChargeFraction)
            return false;

        if (charge < joules)
            return false;

        return _powerCell.TryUseCharge(host, joules);
    }

    /// <summary>
    /// Доля заряда батареи хоста (0–1), либо 0 если батареи нет.
    /// </summary>
    public float GetHostChargeFraction(EntityUid host)
    {
        if (!_powerCell.TryGetBatteryFromSlot(host, out var battery))
            return 0f;

        var (charge, max) = _battery.GetCharge(battery.Value.Owner);
        return max <= 0f ? 0f : charge / max;
    }

    /// <summary>Максимальный заряд батареи хоста в Дж, либо 0.</summary>
    public float GetHostMaxCharge(EntityUid host)
    {
        if (!_powerCell.TryGetBatteryFromSlot(host, out var battery))
            return 0f;

        var (_, max) = _battery.GetCharge(battery.Value.Owner);
        return max;
    }

    /// <summary>
    /// Выключенный тумблер не тратит заряд. Модуль без тумблера всегда включён.
    /// </summary>
    private bool IsModuleDrawingPower(EntityUid moduleUid)
    {
        if (TryComp<FishIpcPassiveHealModuleComponent>(moduleUid, out var heal))
            return heal.Enabled;

        if (TryComp<FishIpcVisionModuleComponent>(moduleUid, out var vision))
            return vision.Enabled;

        if (TryComp<FishIpcMagneticJointsModuleComponent>(moduleUid, out var mag))
            return mag.Enabled;

        if (TryComp<FishIpcSealedJointsModuleComponent>(moduleUid, out var seal))
            return seal.Enabled;

        if (TryComp<FishIpcModulePowerToggleComponent>(moduleUid, out var power))
            return power.Enabled;

        return true;
    }
}
