using Content.Shared.PowerCell.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.PowerCell;

public sealed partial class PowerCellSystem
{
    /// <summary>
    /// Fish: выставляет скорости разряда из прототипа, сохраняя текущее состояние включения.
    /// Нужно апгрейду борга до Mk2: компонент у живого борга уже есть, а перезапись прототипом
    /// сбросила бы <see cref="PowerCellDrawComponent.Enabled"/> и остановила бы разряд.
    /// </summary>
    public void ApplyPrototypeDrawRate(
        Entity<PowerCellDrawComponent?> target,
        EntityPrototype.ComponentRegistryEntry source)
    {
        if (!Resolve(target, ref target.Comp, false) || source.Component is not PowerCellDrawComponent prototype)
            return;

        if (target.Comp.DrawRate.Equals(prototype.DrawRate))
            return;

        target.Comp.DrawRate = prototype.DrawRate;
        Dirty(target, target.Comp);

        if (TryGetBatteryFromSlot(target.Owner, out var battery))
            _battery.RefreshChargeRate(battery.Value.AsNullable());
    }
}
