using Content.Shared._Fish.GridWelding;
using Content.Shared.DoAfter;

namespace Content.Server._Fish.GridWelding;

/// <summary>
/// Stores a welder's first selected edge tile and its current welding action.
/// </summary>
[RegisterComponent, Access(typeof(GridWeldingSystem))]
public sealed partial class GridWeldingComponent : Component
{
    /// <summary>The user who selected the first tile.</summary>
    public EntityUid User;

    /// <summary>The grid containing the first selected edge.</summary>
    public EntityUid Grid;

    /// <summary>The first selected tile in grid coordinates.</summary>
    public Vector2i Tile;

    /// <summary>The active welding action, if any.</summary>
    public DoAfterId? DoAfter;

    /// <summary>The completed action waiting for its turn to merge, also identifying its pending request.</summary>
    public GridWeldingDoAfterEvent? PendingWeld;
}
