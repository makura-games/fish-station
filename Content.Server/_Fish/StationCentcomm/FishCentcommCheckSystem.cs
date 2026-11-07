using Content.Server._Sunrise.StationCentComm;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._Fish.StationCentcomm;

/// <summary>
///     Проверка принадлежности сущности к карте ЦК. Используется геймрулами и ивентами,
///     чтобы они не выбирали ЦК целью.
/// </summary>
public sealed class FishCentcommCheckSystem : EntitySystem
{
    /// <summary>
    ///     True, если сущность находится на карте ЦК.
    ///     Определяет принадлежность по карте, так как <see cref="StationCentCommComponent"/>
    ///     есть и у обычной станции. Для корня станции в Nullspace проверяются её гриды.
    /// </summary>
    public bool IsOnCentcomm(EntityUid uid)
    {
        if (!TryComp(uid, out TransformComponent? xform))
            return false;

        var entityMapId = xform.MapID;

        StationDataComponent? stationData = null;
        if (entityMapId == MapId.Nullspace)
            TryComp(uid, out stationData);

        var query = EntityQueryEnumerator<StationCentCommComponent>();
        while (query.MoveNext(out _, out var centcomm))
        {
            if (centcomm.MapId == MapId.Nullspace)
                continue;

            if (entityMapId != MapId.Nullspace)
            {
                if (centcomm.MapId == entityMapId)
                    return true;

                continue;
            }

            // Корень станции считаем ЦК только если все его гриды на одной карте ЦК;
            // шаттл ЦК не должен переклассифицировать обычную станцию.
            if (stationData == null || stationData.Grids.Count == 0)
                continue;

            var allGridsOnCentcommMap = true;
            foreach (var grid in stationData.Grids)
            {
                if (TryComp(grid, out TransformComponent? gridTransform)
                    && gridTransform.MapID == centcomm.MapId)
                    continue;

                allGridsOnCentcommMap = false;
                break;
            }

            if (allGridsOnCentcommMap)
                return true;
        }

        return false;
    }
}
