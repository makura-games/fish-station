using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._Fish.GridWelding;

public sealed partial class GridWeldingLookupSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MetaDataSystem _metadata = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GridAddEvent>(OnGridAdded);
        SubscribeLocalEvent<MapGridComponent, MapUidChangedEvent>(OnGridMapChanged);
    }

    private void OnGridAdded(GridAddEvent args)
    {
        _metadata.AddFlag(args.EntityUid, MetaDataFlags.ExtraTransformEvents);
    }

    private void OnGridMapChanged(Entity<MapGridComponent> grid, ref MapUidChangedEvent args)
    {
        if (args.NewMap != null || args.OldMap == null)
            return;

        // При удалении грида клиент может получить нового родителя пассажира позднее.
        // Освобождаем прокси, пока они ещё связаны с деревом старого грида.
        var children = args.Transform.ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            var xform = Transform(child);
            if (_lookup.GetCurrentBroadphase(xform)?.Owner == grid.Owner)
                _lookup.RemoveFromEntityTree(child, xform);
        }
    }
}
