// ReSharper disable once CheckNamespace
namespace Content.Shared.Humanoid.Prototypes;

public sealed partial class RandomHumanoidSettingsPrototype
{
    /// <summary>
    /// Включает расы с <c>ExcludeFromRandomSpecies</c> (КПБ) в пул случайного спавна.
    /// </summary>
    [DataField]
    public bool IncludeExcludedSpecies;
}
