using Robust.Shared.Localization;
using Robust.Shared.Serialization;

#pragma warning disable IDE0130 // Необходимо сохранить исходное пространство имён частичного типа.
namespace Content.Shared.Roles;

public sealed partial class AntagPrototype
{
    /// <summary>
    /// Локализационный ключ с правилами этой роли.
    /// </summary>
    [DataField]
    public LocId Rules = string.Empty;
}
