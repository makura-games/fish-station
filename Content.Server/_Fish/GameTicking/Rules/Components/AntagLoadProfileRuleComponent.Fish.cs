// ReSharper disable once CheckNamespace
namespace Content.Server.GameTicking.Rules.Components;

public sealed partial class AntagLoadProfileRuleComponent
{
    /// <summary>
    /// Разрешает расы с <c>ExcludeFromRandomSpecies</c> (КПБ) при загрузке профиля антагониста.
    /// </summary>
    [DataField]
    public bool AllowExcludedSpecies;
}
