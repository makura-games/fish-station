#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Server._Fish.Ipc.Modules;
using Content.Shared._Fish.Ipc.Modules;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish.Ipc;

/// <summary>
/// Общие шаги серверных тестов КПБ. Вызывать с серверного потока.
/// </summary>
public abstract class FishIpcGameTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    protected void DisableDamageVariance()
    {
        var cfg = Server.ResolveDependency<IConfigurationManager>();
        cfg.SetCVar(SunriseCCVars.DamageNegativeVariance, 0f);
        cfg.SetCVar(SunriseCCVars.DamagePositiveVariance, 0f);
    }

    protected EntityUid InstallModule(EntityUid ipc, string prototype)
    {
        var module = SSpawn(prototype);
        var host = SComp<FishIpcModuleHostComponent>(ipc);
        Assert.That(Server.System<SharedContainerSystem>().Insert(module, host.ModuleContainer), Is.True);
        return module;
    }

    protected void Hurt(EntityUid uid, string type, float amount, bool ignoreResistances = true)
    {
        Server.System<DamageableSystem>().TryChangeDamage(
            uid,
            new DamageSpecifier { DamageDict = { [type] = amount } },
            ignoreResistances: ignoreResistances,
            interruptsDoAfters: false,
            ignoreVariance: true);
    }

    protected float Damage(EntityUid uid, string type)
    {
        var damage = Server.System<DamageableSystem>().GetAllDamage(uid);
        return damage.DamageDict.TryGetValue(type, out var value) ? value.Float() : 0f;
    }

    protected Entity<BatteryComponent> Cell(EntityUid ipc)
    {
        Assert.That(Server.System<PowerCellSystem>().TryGetBatteryFromSlot(ipc, out var battery), Is.True);
        return battery!.Value;
    }

    protected void SetCellCharge(EntityUid ipc, float charge)
    {
        Server.System<SharedBatterySystem>().SetCharge(Cell(ipc).AsNullable(), charge);
    }

    protected float CellCharge(EntityUid ipc)
    {
        return Server.System<SharedBatterySystem>().GetCharge(Cell(ipc).Owner).Charge;
    }
}
