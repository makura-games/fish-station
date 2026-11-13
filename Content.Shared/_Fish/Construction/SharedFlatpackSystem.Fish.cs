using Content.Shared.Construction.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Shared.Construction;

public abstract partial class SharedFlatpackSystem
{
    private partial bool CanUnpack(
        Entity<FlatpackComponent> ent,
        EntityUid grid,
        MapGridComponent gridComp,
        Vector2i buildPos,
        EntityUid user,
        int collisionLayer,
        int collisionMask)
    {
        // Fish-edit: гравитационный генератор занимает площадь 3x3, проверяем все её тайлы.
        if (ent.Comp.Entity is not { } entity || entity.Id != "GravityGenerator")
            return true;

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (_anchorable.TileFree(
                        (grid, gridComp),
                        buildPos + new Vector2i(x, y),
                        collisionLayer,
                        collisionMask))
                {
                    continue;
                }

                _popup.PopupEntity(Loc.GetString("flatpack-unpack-no-room"), ent.Owner, user);
                return false;
            }
        }

        return true;
    }
}
