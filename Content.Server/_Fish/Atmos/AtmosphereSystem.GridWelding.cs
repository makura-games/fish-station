using System.Numerics;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Server._Fish.GridWelding;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    // Перенос состояния тайлов изолирован внутри AtmosphereSystem, чтобы не обходить её контракт доступа.
    internal readonly record struct WeldingTileAtmosphere(GasMixture? Air, Hotspot Hotspot,
        float Temperature, float MaxFireTemperature, ChargedElectrovaeEffect ChargedEffect);

    /// <summary>
    /// Captures finite grid tile atmospheres before the donor grid is deleted.
    /// Map atmosphere references and processing caches are deliberately not transferred.
    /// </summary>
    internal Dictionary<Vector2i, WeldingTileAtmosphere> CaptureGridWeldingAtmosphere(EntityUid grid, Matrix3x2 matrix)
    {
        var result = new Dictionary<Vector2i, WeldingTileAtmosphere>();
        if (!TryComp(grid, out GridAtmosphereComponent? atmos))
            return result;

        foreach (var (indices, tile) in atmos.Tiles)
        {
            if (tile.NoGridTile || tile.MapAtmosphere)
                continue;

            result[GridWeldingSystem.TransformTile(indices, matrix)] = new WeldingTileAtmosphere(
                tile.Air?.Clone(), tile.Hotspot, tile.Temperature, tile.MaxFireTemperatureSustained, tile.ChargedEffect);
        }

        return result;
    }

    /// <summary>
    /// Restores gas and thermal state after the merged grid's geometry and airtight entities have moved.
    /// Neighbour references are rebuilt against the surviving grid.
    /// </summary>
    internal void RestoreGridWeldingAtmosphere(EntityUid grid, Dictionary<Vector2i, WeldingTileAtmosphere> snapshot)
    {
        if (snapshot.Count == 0)
            return;

        var atmos = EnsureComp<GridAtmosphereComponent>(grid);
        var visuals = EnsureComp<GasTileOverlayComponent>(grid);
        var mapGrid = Comp<MapGridComponent>(grid);
        var xform = Transform(grid);
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent =
            (grid, atmos, visuals, mapGrid, xform);
        TryComp(xform.MapUid, out MapAtmosphereComponent? mapAtmos);

        foreach (var (indices, state) in snapshot)
        {
            var tile = GetOrNewTile(grid, atmos, indices);
            UpdateTileData(ent, mapAtmos, tile);
            if (tile.MapAtmosphere)
                continue;

            tile.Air = state.Air;
            tile.AirArchived = null;
            tile.ArchivedCycle = 0;
            tile.Temperature = state.Temperature;
            tile.Hotspot = state.Hotspot;
            tile.MaxFireTemperatureSustained = state.MaxFireTemperature;
            tile.ChargedEffect = state.ChargedEffect;
            UpdateAdjacentTiles(ent, tile, activate: true);
            if (tile.Hotspot.Valid)
                atmos.HotspotTiles.Add(tile);
            if (tile.ChargedEffect.Active)
                atmos.ChargedElectrovaeTiles.Add(tile);
            ConsiderSuperconductivity(atmos, tile, true);
            InvalidateVisuals((grid, visuals), indices);
            InvalidateTile((grid, atmos), indices);
            NotifyDeviceTileChanged((grid, atmos, mapGrid), indices);
        }
    }
}
