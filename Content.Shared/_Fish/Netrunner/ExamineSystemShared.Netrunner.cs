using Content.Shared._Fish.Netrunner;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

#pragma warning disable IDE0130 // Fish-edit: Расширяем проверку осмотра для глаза нетраннера из папки Fish.
namespace Content.Shared.Examine;

public abstract partial class ExamineSystemShared
{
    [Dependency] private SharedMapSystem _netrunnerMaps = default!;
    [Dependency] private StationAiVisionSystem _netrunnerVision = default!;
    [Dependency] private EntityQuery<NetrunnerUserComponent> _netrunnerUserQuery = default!;
    [Dependency] private EntityQuery<NetrunnerRemoteEyeComponent> _netrunnerEyeQuery = default!;

    private bool TryCanExamineFromNetrunnerEye(EntityUid examiner, MapCoordinates target, out bool canExamine)
    {
        canExamine = false;
        if (!_netrunnerUserQuery.TryComp(examiner, out var user)
            || user.RemoteEye is not { } remote
            || Deleted(remote)
            || !_netrunnerEyeQuery.TryComp(remote, out var viewer))
            return false;

        var eyeCoords = _transform.GetMapCoordinates(remote);
        if (eyeCoords.MapId != target.MapId || (eyeCoords.Position - target.Position).Length() > ExamineRange)
            return true;

        if (!_netrunnerMaps.TryFindGridAt(target, out var gridUid, out var grid)
            || !TryComp<BroadphaseComponent>(gridUid, out var broadphase))
            return true;

        var tile = _netrunnerMaps.CoordinatesToTile(gridUid, grid, target);
        canExamine = _netrunnerVision.IsNetrunnerAccessible((gridUid, broadphase, grid), tile, viewer.Station, user.Chair);
        return true;
    }
}
