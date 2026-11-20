using Content.Shared.Access.Components;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемой upstream-системе.
namespace Content.Shared.Access.Systems;

public sealed partial class AccessReaderSystem
{
    /*
     * Alert-level access groups and their authorization rules.
     */

    private bool IsAccessAllowedByAlertLevel(
        ICollection<ProtoId<AccessLevelPrototype>> access,
        AccessReaderComponent reader)
    {
        if (reader.DenyTags.Overlaps(access))
            return false;

        if (reader.Group is { } group && IsAlertAccessGroupAllowed(access, group))
            return true;

        foreach (var additionalGroup in reader.AdditionalGroups)
        {
            if (IsAlertAccessGroupAllowed(access, additionalGroup))
                return true;
        }

        return false;
    }

    private bool IsAlertAccessGroupAllowed(
        ICollection<ProtoId<AccessLevelPrototype>> access,
        ProtoId<AccessGroupPrototype> group)
    {
        if (!ProtoMan.Resolve(group, out var accessGroup))
            return false;

        foreach (var tag in accessGroup.Tags)
        {
            if (access.Contains(tag))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Updates temporary access groups for the primary and additional alert levels.
    /// </summary>
    public void UpdateAccess(
        Entity<AccessReaderComponent> ent,
        string primaryLevel,
        IReadOnlyList<string> activeLevels,
        IReadOnlyCollection<ProtoId<AccessGroupPrototype>>? globalGroups = null)
    {
        // Fish-start - отсутствие группы кода не должно создавать пустой ProtoId.
        if (TryGetAlertAccessGroup(ent.Comp, primaryLevel, out var primaryGroup))
            ent.Comp.Group = primaryGroup;
        else
            ent.Comp.Group = null;
        // Fish-end
        ent.Comp.AdditionalGroups.Clear();

        foreach (var level in activeLevels)
        {
            if (string.Equals(level, primaryLevel, StringComparison.OrdinalIgnoreCase)
                || !TryGetAlertAccessGroup(ent.Comp, level, out var group))
            {
                continue;
            }

            AddAdditionalAccessGroup(ent.Comp, group);
        }

        if (globalGroups != null)
        {
            foreach (var group in globalGroups)
            {
                AddAdditionalAccessGroup(ent.Comp, group);
            }
        }

        Dirty(ent);
    }

    private static bool TryGetAlertAccessGroup(
        AccessReaderComponent reader,
        string level,
        out ProtoId<AccessGroupPrototype> group)
    {
        return reader.AlertAccesses.TryGetValue(level, out group)
               || reader.AlertAccesses.TryGetValue(level.ToLowerInvariant(), out group)
               || string.Equals(level, "DeltaNuke", StringComparison.OrdinalIgnoreCase)
               && reader.AlertAccesses.TryGetValue("delta", out group);
    }

    private static void AddAdditionalAccessGroup(
        AccessReaderComponent reader,
        ProtoId<AccessGroupPrototype> group)
    {
        if (reader.Group != group)
            reader.AdditionalGroups.Add(group);
    }
}
