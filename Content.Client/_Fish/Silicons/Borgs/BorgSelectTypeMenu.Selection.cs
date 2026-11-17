// Необходимо поскольку этот partial класс расширяет ванильный тип и должен сохранять исходный namespace несмотря на расположение файла в _Fish
#pragma warning disable IDE0130 // Namespace does not match folder structure
using System.Linq;
using Content.Shared.Silicons.Borgs;
using Robust.Shared.Prototypes;

namespace Content.Client.Silicons.Borgs;

public sealed partial class BorgSelectTypeMenu
{
    /// <summary>
    /// Fish: специализации, доступные для ручного выбора: скрытые типы (Mk2)
    /// не показываются — они недоступны для ручного выбора. Порядок кнопок —
    /// по локализованному имени.
    /// </summary>
    private IEnumerable<BorgTypePrototype> GetSelectableBorgTypes()
    {
        return _prototypeManager.EnumeratePrototypes<BorgTypePrototype>()
            .Where(type => !type.HideInMenu)
            .OrderBy(PrototypeName);
    }

    /// <summary>
    /// Fish: локализованное имя типа; ключ строится через <see cref="BorgTypePrototype.GetLocKey"/>.
    /// </summary>
    private static string PrototypeName(BorgTypePrototype prototype)
    {
        return Loc.GetString(prototype.GetLocKey("name"));
    }

    /// <summary>
    /// Fish: локализованное описание типа; ключ строится через <see cref="BorgTypePrototype.GetLocKey"/>.
    /// </summary>
    private static string GetBorgTypeDescription(BorgTypePrototype prototype)
    {
        return Loc.GetString(prototype.GetLocKey("desc"));
    }
}
