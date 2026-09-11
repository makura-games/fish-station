using System.Linq;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Shared._Fish.Achievements;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Nutrition;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Tag;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Achievements;

public sealed partial class AchievementConditionSystem
{
    partial void InitializeExtended()
    {
        SubscribeLocalEvent<AchievementTrackedComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<AfterAntagEntitySelectedEvent>(OnAntagSelected);
        SubscribeLocalEvent<AchievementTrackedComponent, IngestedEvent>(OnIngested);
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
    }

    private async void OnMindRemoved(EntityUid uid, AchievementTrackedComponent tracked, MindRemovedMessage args)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        if (args.TransferEntity is not { } transfer || !HasComp<GhostComponent>(transfer))
            return;

        await _achievements.ContributeAsync(
            actor.PlayerSession,
            AchievementConditionKeys.BecameGhost,
            new AchievementTriggerContext(
                EventKey: $"ghost:{actor.PlayerSession.UserId}:{_timing.CurTick}",
                RequireInRound: false));
    }

    private void OnAntagSelected(ref AfterAntagEntitySelectedEvent ev)
    {
        if (ev.Session == null)
            return;

        foreach (var antagRole in ev.Def.PrefRoles)
        {
            _ = _achievements.ContributeAsync(
                ev.Session,
                AchievementConditionKeys.AntagSelected,
                new AchievementTriggerContext(
                    AntagPrototypeId: antagRole.Id,
                    EventKey: $"antag:{ev.Session.UserId}:{antagRole.Id}"));
        }
    }

    private async void OnIngested(EntityUid uid, AchievementTrackedComponent tracked, IngestedEvent args)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        var itemProto = GetPrototypeId(args.Target);
        if (itemProto == null)
            return;

        await _achievements.ContributeAsync(
            actor.PlayerSession,
            AchievementConditionKeys.ItemIngest,
            new AchievementTriggerContext(
                EntityPrototypeId: itemProto,
                EventKey: $"ingest:{GetNetEntity(args.Target)}:{actor.PlayerSession.UserId}"));
    }

    private async void OnRoleAdded(RoleAddedEvent args)
    {
        if (!TryComp<MindComponent>(args.MindId, out var mind) || mind.UserId is not { } userId)
            return;

        if (!_players.TryGetSessionById(userId, out var session))
            return;

        var roles = mind.MindRoleContainer.ContainedEntities.ToArray();
        foreach (var roleEnt in roles)
        {
            if (!TryComp<MindRoleComponent>(roleEnt, out var role))
                continue;

            if (role.JobPrototype is { } job)
            {
                await _achievements.ContributeAsync(
                    session,
                    AchievementConditionKeys.RoleAdded,
                    new AchievementTriggerContext(
                        JobId: job.Id,
                        EventKey: $"role:{userId}:{job.Id}"));
            }

            if (role.AntagPrototype is { } antag)
            {
                await _achievements.ContributeAsync(
                    session,
                    AchievementConditionKeys.AntagSelected,
                    new AchievementTriggerContext(
                        AntagPrototypeId: antag.Id,
                        EventKey: $"role-antag:{userId}:{antag.Id}"));
            }
        }
    }
}
