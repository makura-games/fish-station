// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/space-sunrise/space-station-14/blob/master/CLA.txt

using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease;

public sealed partial class GiveDiseaseImmunityEntityEffectSystem : EntityEffectSystem<TransformComponent, GiveDiseaseImmunity>
{
    [Dependency] private EntityManager _entityManager = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<GiveDiseaseImmunity> args)
    {
        _entityManager.EnsureComponent<DiseaseImmuneComponent>(entity.Owner);
    }
}

public sealed partial class GiveDiseaseImmunity : EntityEffectBase<GiveDiseaseImmunity>
{
    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("entity-effect-guidebook-give-disease-immunity", ("chance", Probability));
    }
}
