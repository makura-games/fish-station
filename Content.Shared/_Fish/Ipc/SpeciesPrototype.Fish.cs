using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Humanoid.Prototypes;

public sealed partial class SpeciesPrototype
{
    /// <summary>
    /// Запретить случайный выбор вида в <c>RandomSpecies</c> и спавн на ролях призраков/антагонистов.
    /// </summary>
    [DataField]
    public bool ExcludeFromRandomSpecies { get; private set; }
}
