using Content.Shared.Research.Components;

namespace Content.Shared.Research.Systems;

public abstract partial class SharedResearchSystem
{
    private void UpdateFishTechnologyChoices(EntityUid uid, TechnologyDatabaseComponent database)
    {
        var available = GetAvailableTechnologies(uid, database);
        available.Sort(static (left, right) => string.Compare(left.ID, right.ID, StringComparison.Ordinal));
        database.CurrentTechnologyCards.Clear();
        foreach (var technology in available)
            database.CurrentTechnologyCards.Add(technology.ID);
        Dirty(uid, database);
    }
}
