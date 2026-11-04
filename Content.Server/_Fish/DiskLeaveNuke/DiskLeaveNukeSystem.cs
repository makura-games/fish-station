using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server._Sunrise.StationCentComm;
using Content.Shared._Fish.DiskLeaveNuke;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Mobs.Components;
using Content.Shared.Radio;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.DiskLeaveNuke;

public sealed class DiskLeaveNukeSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<DiskLeaveNukeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextCheck)
                continue;

            comp.NextCheck = _timing.CurTime + comp.CheckInterval;

            if (!IsOutside(uid))
            {
                comp.StrikeTime = null;
                continue;
            }

            if (comp.StrikeTime == null)
            {
                comp.StrikeTime = _timing.CurTime + comp.Delay;
                Say((uid, comp), Format(uid, comp, comp.WarningMessage, comp.WarningNoHolder));
                continue;
            }

            if (_timing.CurTime >= comp.StrikeTime)
                Strike((uid, comp));
        }
    }

    private bool IsOutside(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.MapID == MapId.Nullspace)
            return false;

        if (xform.GridUid is { } grid && (HasComp<StationMemberComponent>(grid) || HasComp<EmergencyShuttleComponent>(grid)))
            return false;

        return !IsCentComm(xform.MapID);
    }

    private bool IsCentComm(MapId mapId)
    {
        var query = EntityQueryEnumerator<StationCentCommComponent>();
        while (query.MoveNext(out _, out var centComm))
        {
            if (centComm.MapId == mapId)
                return true;
        }

        return false;
    }

    private string Format(EntityUid uid, DiskLeaveNukeComponent comp, LocId withHolder, LocId noHolder)
    {
        var minutes = (int) comp.Delay.TotalMinutes;
        return GetHolderName(uid) is { } holder
            ? Loc.GetString(withHolder, ("holder", holder), ("minutes", minutes))
            : Loc.GetString(noHolder, ("minutes", minutes));
    }

    private string? GetHolderName(EntityUid uid)
    {
        var current = Transform(uid).ParentUid;
        while (current.IsValid())
        {
            if (HasComp<MobStateComponent>(current))
                return Name(current);

            current = Transform(current).ParentUid;
        }

        return null;
    }

    private void Say(Entity<DiskLeaveNukeComponent> ent, string message)
    {
        _chat.TrySendInGameICMessage(ent, message, InGameICChatType.Speak, false, ignoreActionBlocker: true);
        _radio.SendRadioMessage(ent, message, _proto.Index(ent.Comp.RadioChannel), ent);
    }

    private void Strike(Entity<DiskLeaveNukeComponent> ent)
    {
        var comp = ent.Comp;
        var coords = _transform.GetMapCoordinates(ent);

        Say(ent, Format(ent, comp, comp.StrikeMessage, comp.StrikeNoHolder));

        _adminLog.Add(LogType.Explosion, LogImpact.Extreme,
            $"{ToPrettyString(ent):entity} was hit by bluespace artillery after leaving the station at {coords}");

        _explosion.QueueExplosion(
            coords,
            comp.ExplosionType,
            comp.TotalIntensity,
            comp.Slope,
            comp.MaxIntensity,
            null,
            comp.TileBreakScale,
            canCreateVacuum: comp.CanCreateVacuum);

        QueueDel(ent);
    }
}
