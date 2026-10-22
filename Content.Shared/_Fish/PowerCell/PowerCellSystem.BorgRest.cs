using Content.Shared.PowerCell.Components;
using JetBrains.Annotations;

#pragma warning disable IDE0130 // Необходимо поскольку этот partial класс расширяет ванильный тип и должен сохранять исходный namespace несмотря на расположение файла в _Fish
namespace Content.Shared.PowerCell;

public sealed partial class PowerCellSystem
{
    /// <summary>
    /// Выставляет скорость разряда и пересчитывает заряд батареи тем же путём, что и
    /// SetDrawEnabled, сохраняя состояние включения. Нужно механике отдыха боргов Mk2:
    /// <see cref="PowerCellDrawComponent"/> закрыт Access'ом для этой системы, поэтому
    /// изменение DrawRate выполняется здесь, внутри partial PowerCellSystem.
    /// </summary>
    [PublicAPI]
    public void SetDrawRate(Entity<PowerCellDrawComponent?> target, float drawRate)
    {
        if (!Resolve(target, ref target.Comp, false) || target.Comp.DrawRate.Equals(drawRate))
            return;

        target.Comp.DrawRate = drawRate;
        Dirty(target, target.Comp);

        if (TryGetBatteryFromSlot(target.Owner, out var battery))
            _battery.RefreshChargeRate(battery.Value.AsNullable());
    }
}
