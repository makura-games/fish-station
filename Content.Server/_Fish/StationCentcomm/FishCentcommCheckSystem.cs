using Content.Server._Sunrise.StationCentComm;
using Content.Shared.Station.Components;
using Content.Shared._Sunrise.Shuttles;
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
        // NanotrasenCentralCommand получает этот компонент через BaseCentCommShuttles.
        // Поэтому станция распознаётся даже до назначения MapId.
        if (IsCentcommStation(uid))
            return true;

        if (!TryComp(uid, out TransformComponent? xform))
            return false;

        // Учитываем принадлежность грида к станции ЦК независимо от его текущей карты.
        var gridUid = xform.GridUid ?? uid;
        if (HasComp<CentCommShuttleComponent>(gridUid)
            && TryComp(gridUid, out StationMemberComponent? stationMember)
            && IsCentcommStation(stationMember.Station))
            return true;

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

            if (stationData == null || stationData.Grids.Count == 0)
                continue;

            var allGridsOnCentcommMap = true;
            var allNonShuttleGridsOnCentcommMap = true;
            var hasNonShuttleGrid = false;

            foreach (var grid in stationData.Grids)
            {
                var isOnCentcommMap = TryComp(grid, out TransformComponent? gridTransform)
                    && gridTransform.MapID == centcomm.MapId;

                if (!isOnCentcommMap)
                    allGridsOnCentcommMap = false;

                if (HasComp<CentCommShuttleComponent>(grid))
                    continue;

                hasNonShuttleGrid = true;

                if (!isOnCentcommMap)
                    allNonShuttleGridsOnCentcommMap = false;
            }

            if (hasNonShuttleGrid
                ? allNonShuttleGridsOnCentcommMap
                : allGridsOnCentcommMap)
                return true;
        }

        return false;
    }

    private bool IsCentcommStation(EntityUid station)
    {
        return TryComp(station, out MetaDataComponent? metadata)
            && metadata.EntityPrototype?.ID == "NanotrasenCentralCommand";
    }
}
