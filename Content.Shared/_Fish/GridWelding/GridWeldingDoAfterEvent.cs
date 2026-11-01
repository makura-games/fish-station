using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.GridWelding;

/// <summary>
/// Identifies the two selected edge tiles throughout a grid welding action.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class GridWeldingDoAfterEvent : DoAfterEvent
{
    /// <summary>The first selected grid.</summary>
    [DataField]
    public NetEntity FirstGrid;

    /// <summary>The second selected grid.</summary>
    [DataField]
    public NetEntity SecondGrid;

    /// <summary>The selected tile on the first grid.</summary>
    [DataField]
    public Vector2i FirstTile;

    /// <summary>The selected tile on the second grid.</summary>
    [DataField]
    public Vector2i SecondTile;

    public GridWeldingDoAfterEvent(NetEntity firstGrid, NetEntity secondGrid, Vector2i firstTile, Vector2i secondTile)
    {
        FirstGrid = firstGrid;
        SecondGrid = secondGrid;
        FirstTile = firstTile;
        SecondTile = secondTile;
    }

    private GridWeldingDoAfterEvent()
    {
    }

    public override DoAfterEvent Clone() => this;
}
