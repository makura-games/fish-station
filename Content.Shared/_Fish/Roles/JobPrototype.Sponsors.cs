// Необходимо поскольку этот partial класс расширяет ванильный тип и должен сохранять исходный namespace несмотря на расположение файла в _Fish
#pragma warning disable IDE0130 // Namespace does not match folder structure
using Robust.Shared.Prototypes;

namespace Content.Shared.Roles;

public sealed partial class JobPrototype
{
    /// <summary>
    /// Дополнительные ID ролей, наличие которых у спонсора открывает доступ к этой должности.
    /// Нужно для переименованных ролей, чтобы старые вайтлисты продолжали работать.
    /// </summary>
    [DataField]
    public string[] SponsorAliases = [];
}
