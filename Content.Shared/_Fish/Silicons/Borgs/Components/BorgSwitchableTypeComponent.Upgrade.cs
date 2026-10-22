// Необходимо поскольку этот partial класс расширяет ванильный тип и должен сохранять исходный namespace несмотря на расположение файла в _Fish
#pragma warning disable IDE0130 // Namespace does not match folder structure
using Content.Shared.DoAfter;

namespace Content.Shared.Silicons.Borgs.Components;

public sealed partial class BorgSwitchableTypeComponent : Component
{
    // Fish: DoAfter текущего апгрейда до Mk2 через BorgUpgradeModule: пока он запущен,
    // второй апгрейд той же цели блокируется (дедупликация DoAfter работает per-user
    // и от второго игрока с отдельным предметом не спасает).
    // Сознательно не DataField: DoAfterId — runtime-идентификатор, YAML-сериализатор ему не нужен.
    /// <summary>
    /// Fish: запущенный апгрейд до Mk2, либо <c>null</c>.
    /// </summary>
    [ViewVariables]
    public DoAfterId? ActiveUpgradeDoAfter;
}
