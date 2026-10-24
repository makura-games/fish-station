using Content.Server._Sunrise.ImmortalGrid;
using Content.Server.Salvage.Expeditions;
using Content.Server.Shuttles.Components;
using Content.Shared._Sunrise.UnbuildableGrid;
using Content.Shared.Cargo.Components;
using Content.Shared.Station.Components;
using Content.Shared.Tiles;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server._Fish.GridWelding;

public sealed partial class GridWeldingSystem
{
    // Удаление грида при слиянии не должно обходить защиту строительства или менять идентификатор служебного шаттла.
    private bool IsProtectedGrid(EntityUid grid)
    {
        return HasComp<ProtectedGridComponent>(grid) || HasComp<UnbuildableGridComponent>(grid) ||
               HasComp<ImmortalGridComponent>(grid);
    }

    private bool HasPersistentRole(EntityUid grid)
    {
        return HasComp<ArrivalsShuttleComponent>(grid) || HasComp<EmergencyShuttleComponent>(grid) ||
               HasComp<StationCargoShuttleComponent>(grid) || HasComp<StationEmergencyShuttleComponent>(grid) ||
               HasComp<CargoShuttleComponent>(grid) || HasComp<SalvageShuttleComponent>(grid);
    }

    private void OnGridSplit(ref GridSplitEvent args)
    {
        var origin = EnsureComp<GridWeldingOriginComponent>(args.Grid);
        if (!origin.OriginalGrid.IsValid())
            origin.OriginalGrid = args.Grid;

        foreach (var fragment in args.NewGrids)
            EnsureComp<GridWeldingOriginComponent>(fragment).OriginalGrid = origin.OriginalGrid;
    }

    private EntityUid GetSurvivingGrid(EntityUid first, EntityUid second)
    {
        var a = Comp<PhysicsComponent>(first);
        var b = Comp<PhysicsComponent>(second);
        if (a.BodyType == BodyType.Static)
            return first;
        if (b.BodyType == BodyType.Static)
            return second;

        var roleA = HasPersistentRole(first);
        var roleB = HasPersistentRole(second);
        if (roleA != roleB)
            return roleA ? first : second;

        var stationA = HasComp<StationMemberComponent>(first);
        var stationB = HasComp<StationMemberComponent>(second);
        if (stationA != stationB)
            return stationA ? first : second;

        if (TryComp(first, out GridWeldingOriginComponent? originA) &&
            TryComp(second, out GridWeldingOriginComponent? originB) && originA.OriginalGrid == originB.OriginalGrid)
        {
            if (originA.OriginalGrid == first)
                return first;
            if (originA.OriginalGrid == second)
                return second;
        }

        return b.FixturesMass > a.FixturesMass ? second : first;
    }
}
