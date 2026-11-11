using System.Linq;
using Content.Shared._Fish.Achievements;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Robust.Shared.Player;

namespace Content.Server._Fish.Achievements;

public sealed partial class AchievementConditionSystem
{
    [Dependency] private SharedObjectivesSystem _objectives = default!;

    private async void ProcessRoundEndObjectives(RoundEndMessageEvent ev)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } ent)
                continue;

            if (!_mind.TryGetMind(ent, out var mindId, out var mind))
                continue;

            foreach (var objectiveEnt in mind.Objectives)
            {
                if (!TryComp<ObjectiveComponent>(objectiveEnt, out _))
                    continue;

                if (!_objectives.IsCompleted(objectiveEnt, (mindId, mind)))
                    continue;

                var objectiveProto = MetaData(objectiveEnt).EntityPrototype?.ID;
                if (objectiveProto == null)
                    continue;

                await _achievements.ContributeAsync(
                    session,
                    AchievementConditionKeys.ObjectiveComplete,
                    new AchievementTriggerContext(
                        ObjectivePrototypeId: objectiveProto,
                        EventKey: $"obj:{session.UserId}:{objectiveProto}:{ev.RoundId}",
                        RequireInRound: false));
            }

            // Все objectives выполнены (mission complete).
            if (mind.Objectives.Count > 0 &&
                mind.Objectives.All(o => _objectives.IsCompleted(o, (mindId, mind))))
            {
                await _achievements.ContributeAsync(
                    session,
                    AchievementConditionKeys.ObjectiveComplete,
                    new AchievementTriggerContext(
                        ObjectivePrototypeId: "*",
                        EventKey: $"obj-all:{session.UserId}:{ev.RoundId}",
                        RequireInRound: false));
            }
        }
    }
}
