using System.Collections.Generic;
using Content.Server.Antag;
using Content.Server.Mind;
using Content.Shared._Fish.Antag;
using Content.Shared.Roles.Components;
using Robust.Shared.Player;

namespace Content.Server._Fish.Antag;

public sealed partial class FishAntagRulesServerSystem : EntitySystem
{
    [Dependency] private MindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AfterAntagEntitySelectedEvent>(OnAntagSelected);
    }

    private void OnAntagSelected(ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Session is not { } session || !_mind.TryGetMind(session, out _, out var mind))
            return;

        var rules = new List<string>();
        foreach (var role in mind.MindRoleContainer.ContainedEntities)
        {
            if (!TryComp<MindRoleComponent>(role, out var mindRole) || mindRole.AntagPrototype is not { } antagId)
                continue;

            var locId = ProtoMan.Index(antagId).Rules?.Id;
            if (!string.IsNullOrWhiteSpace(locId) && !rules.Contains(locId))
                rules.Add(locId);
        }

        if (rules.Count > 0)
            RaiseNetworkEvent(new FishAntagRulesEvent([.. rules]), session);
    }
}
