using Content.Server._Fish.Objectives.Components;
using Content.Shared.Objectives.Components;

namespace Content.Server._Fish.Objectives.Systems;

public sealed partial class AnimalRoleRequirementSystem : EntitySystem
{
    [Dependency] private AnimalObjectivesSystem _animalObjectives = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnimalRoleRequirementComponent, RequirementCheckEvent>(OnCheck);
    }

    private void OnCheck(EntityUid uid, AnimalRoleRequirementComponent comp, ref RequirementCheckEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.Mind.OwnedEntity is not { } owned || !_animalObjectives.IsEligible(owned))
            args.Cancelled = true;
    }
}
