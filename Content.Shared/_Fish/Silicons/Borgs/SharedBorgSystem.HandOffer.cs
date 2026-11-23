// Необходимо поскольку этот partial класс расширяет ванильный тип и должен сохранять исходный namespace несмотря на расположение файла в _Fish
#pragma warning disable IDE0130
using Content.Shared.Silicons.Borgs.Components;

namespace Content.Shared.Silicons.Borgs;

public abstract partial class SharedBorgSystem
{
    /// <summary>
    /// Предмет принадлежит модулю борга: лежит в его контейнере, является установленным
    /// модулем этого держателя или занимает руку, которую выдал модуль.
    /// Такие предметы — внутренности борга, их нельзя передать из рук в руки.
    /// </summary>
    public bool IsBorgModuleContent(EntityUid holder, EntityUid item)
    {
        if (IsInsideBorgModule(item))
            return true;

        if (TryComp<BorgModuleComponent>(item, out var module) && module.InstalledEntity == holder)
            return true;

        if (!_hands.IsHolding(holder, item, out var handId) ||
            !TryComp<BorgChassisComponent>(holder, out var chassis))
            return false;

        // Руки ещё не созданы: проверять id слотов нечего.
        if (chassis.ModuleContainer is not { } modules)
            return false;

        foreach (var moduleEnt in modules.ContainedEntities)
        {
            if (!TryComp<ItemBorgModuleComponent>(moduleEnt, out var itemModule))
                continue;

            // Тот же id, что SharedBorgSystem выдаёт в ProvideItems: "{NetEntity}-hand-{index}".
            for (var i = 0; i < itemModule.Hands.Count; i++)
            {
                if (handId == $"{GetNetEntity(moduleEnt)}-hand-{i}")
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Предмет лежит внутри сущности с <see cref="BorgModuleComponent"/>
    /// (например, в holding_container невыбранного модуля).
    /// </summary>
    private bool IsInsideBorgModule(EntityUid item)
    {
        var current = item;

        // Глубина с запасом: рука → борг → шкаф, циклов у контейнеров не бывает.
        for (var depth = 0; depth < 16; depth++)
        {
            if (!_container.TryGetContainingContainer(current, out var container))
                return false;

            if (HasComp<BorgModuleComponent>(container.Owner))
                return true;

            if (container.Owner == current)
                return false;

            current = container.Owner;
        }

        return false;
    }
}
