using Content.Shared._Fish.Effects;

namespace Content.Shared.Botany.Components;

public sealed partial class PlantDataComponent
{
    /// <summary>Вид частиц при сборе урожая этой культуры.</summary>
    [DataField]
    public FishParticleKind HarvestParticle = FishParticleKind.Leaves;
}
