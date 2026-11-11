using Content.Server._Fish.Objectives.Components;
using Content.Server.GameTicking;
using Content.Server.Objectives;
using Content.Shared._Fish.Objectives.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Objectives.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Fish.Objectives.Systems;

public sealed partial class AnimalObjectivesSystem : EntitySystem
{
    private static readonly EntProtoId AnimalObjectivesRuleId = "AnimalObjectives";

    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ObjectivesSystem _objectives = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<MindContainerComponent, MindAddedMessage>(OnMindAdded);
    }

    public bool IsEligible(EntityUid uid, AnimalObjectivesRuleComponent? rule = null)
    {
        if (MetaData(uid).EntityPrototype is not { } proto)
            return false;

        rule ??= TryGetActiveRule(out var activeRule)
            ? activeRule
            : TryGetRulePrototype(out var protoRule) ? protoRule : null;

        return rule != null && rule.EligiblePrototypes.Contains(proto.ID);
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        // Ленивый старт по паттерну GenericAntag: правило поднимается при MindAdded / здесь,
        // если на карте уже есть eligible животные. Не в SubGamemodes — иначе двойной путь
        // и лишний GameRule без животных (ломает тесты вроде RuleMaxTimeRestartTest при автостарте).
        AssignObjectivesToExistingMinds();
    }

    private void OnMindAdded(EntityUid uid, MindContainerComponent comp, MindAddedMessage args)
    {
        TryAssignObjectives(uid, args.Mind.Owner, args.Mind.Comp);
    }

    private void AssignObjectivesToExistingMinds()
    {
        var query = EntityQueryEnumerator<MindContainerComponent>();
        while (query.MoveNext(out var uid, out var mindContainer))
        {
            if (mindContainer.Mind is not { } mindId || !TryComp<MindComponent>(mindId, out var mind))
                continue;

            TryAssignObjectives(uid, mindId, mind);
        }
    }

    private void TryAssignObjectives(EntityUid uid, EntityUid mindId, MindComponent mind)
    {
        if (!IsEligible(uid))
            return;

        EnsureRuleStarted();

        // Трекер всегда на текущем теле — в т.ч. при переносе Mind / повторном MindAdded.
        EnsureComp<AnimalObjectiveTrackerComponent>(uid);

        if (HasAnimalObjectives(mind))
        {
            RegisterMind(mindId);
            return;
        }

        var assigned = AssignObjectives(mindId, mind);
        if (assigned <= 0)
            return;

        RegisterMind(mindId);
        Log.Debug($"Assigned {assigned} animal objective(s) to {ToPrettyString(uid)}");
    }

    private int AssignObjectives(EntityUid mindId, MindComponent mind)
    {
        if (!TryGetActiveRule(out var rule))
            return 0;

        var difficulty = 0f;
        var assignedCount = 0;

        foreach (var set in rule.Sets)
        {
            if (!_random.Prob(set.Prob))
                continue;

            for (var pick = 0; pick < set.MaxPicks && rule.MaxDifficulty > difficulty; pick++)
            {
                var remaining = rule.MaxDifficulty - difficulty;
                if (_objectives.GetRandomObjective(mindId, mind, set.Groups, remaining) is not { } objective)
                    continue;

                _mind.AddObjective(mindId, mind, objective);
                difficulty += Comp<ObjectiveComponent>(objective).Difficulty;
                assignedCount++;
            }
        }

        return assignedCount;
    }

    private void RegisterMind(EntityUid mindId)
    {
        if (!TryGetActiveRule(out var rule) || rule.Minds.Contains(mindId))
            return;

        rule.Minds.Add(mindId);
    }

    private bool TryGetActiveRule(out AnimalObjectivesRuleComponent rule)
    {
        rule = default!;

        var query = EntityQueryEnumerator<AnimalObjectivesRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (!_gameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            rule = comp;
            return true;
        }

        return false;
    }

    private bool TryGetRulePrototype(out AnimalObjectivesRuleComponent rule)
    {
        rule = default!;

        if (!_proto.TryIndex(AnimalObjectivesRuleId, out EntityPrototype? ruleProto))
            return false;

        if (!ruleProto.TryGetComponent(out AnimalObjectivesRuleComponent? config, EntityManager.ComponentFactory)
            || config == null)
            return false;

        rule = config;
        return true;
    }

    private bool HasAnimalObjectives(MindComponent mind)
    {
        foreach (var objective in mind.Objectives)
        {
            if (HasComp<AnimalRoleRequirementComponent>(objective))
                return true;
        }

        return false;
    }

    private void EnsureRuleStarted()
    {
        if (TryGetActiveRule(out _))
            return;

        var rule = _gameTicker.AddGameRule(AnimalObjectivesRuleId);
        _gameTicker.StartGameRule(rule);
    }
}
