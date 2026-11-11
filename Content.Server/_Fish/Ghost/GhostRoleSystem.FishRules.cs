using System.Collections.Generic;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Необходимо сохранить исходное пространство имён частичного типа.
namespace Content.Server.Ghost.Roles;

public sealed partial class GhostRoleSystem
{
    private string GetFishRoleRules(List<ProtoId<AntagPrototype>> antags)
    {
        if (antags.Count == 0)
            return string.Empty;

        var rules = new List<string>();
        foreach (var antagId in antags)
        {
            var locId = ProtoMan.Index(antagId).Rules?.Id;
            if (string.IsNullOrWhiteSpace(locId))
                continue;

            var text = Loc.GetString(locId);
            if (!rules.Contains(text))
                rules.Add(text);
        }

        return string.Join("\n\n", rules);
    }
}
