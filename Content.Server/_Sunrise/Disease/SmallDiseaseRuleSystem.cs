using System.Linq;
using Content.Server.Antag;
using Content.Shared._Sunrise.Disease;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Content.Shared.Store;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mind.Components;
using Robust.Shared.Map;
using Content.Server.Chat.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Disease;

public sealed partial class SmallDiseaseRuleSystem : GameRuleSystem<SmallDiseaseRuleComponent>
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ChatSystem _chatSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    protected override void Started(EntityUid uid, SmallDiseaseRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        Timer.Spawn(TimeSpan.FromMinutes(10), () =>
        {
            var message = Loc.GetString("disease-biohazard-announcement");
            var sender = Loc.GetString("disease-biohazard-announcement-sender");

            _chatSystem.DispatchGlobalAnnouncement(message, sender, playDefault: true, colorOverride: Color.Red);
        });

        // Spawn decoy disease entity
        var diseaseUid = EntityManager.SpawnEntity(component.DiseasePrototype, MapCoordinates.Nullspace);

        if (!TryComp<DiseaseRoleComponent>(diseaseUid, out var diseaseComp))
        {
            return;
        }

        // 1. Find potential victims (Humanoid, Has Mind, Not Dead, Not Sick)
        var query = EntityQueryEnumerator<HumanoidProfileComponent, MindContainerComponent, MobStateComponent, TransformComponent>();
        var candidates = new List<EntityUid>();

        while (query.MoveNext(out var entity, out _, out _, out _, out var xform))
        {
            if (HasComp<SickComponent>(entity))
                continue;

            // Simple check to ensure they are on a station/grid generally
            if (xform.GridUid == null)
                continue;

            candidates.Add(entity);
        }

        if (candidates.Count == 0)
            return;

        // 2. Configure Symptoms
        // Always add default symptom
        diseaseComp.Symptoms.TryAdd(component.DefaultSymptom, new SymptomData(2, 5));

        var currentPoints = 0;
        var availableSymptoms = _prototypeManager.EnumeratePrototypes<ListingPrototype>()
            .Where(listing => listing.Categories.Contains(component.SymptomCategory))
            .ToList();

        // Randomly add until points target reached
        for (var i = 0; i < 20 && currentPoints < component.TargetSymptomPoints; i++)
        {
            if (availableSymptoms.Count == 0)
                break;

            var pick = _random.Pick(availableSymptoms);
            var cost = pick.Cost.GetValueOrDefault("DiseasePoints", 0);

            // Avoid adding same symptom twice
            if (!diseaseComp.Symptoms.ContainsKey(pick.ID))
            {
                diseaseComp.Symptoms.Add(pick.ID, new SymptomData(1, 5));
                currentPoints += cost.Int();
            }
        }

        // 3. Infect Targets
        _random.Shuffle(candidates);
        var targetCount = Math.Min(component.TargetInfectedCount, candidates.Count);

        for (var i = 0; i < targetCount; i++)
        {
            var victim = candidates[i];
            var sick = EnsureComp<SickComponent>(victim);
            sick.owner = diseaseUid;
            diseaseComp.Infected.Add(victim);
        }
    }
}


