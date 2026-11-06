using Content.Server.Humanoid.Components;
using Content.Server.Humanoid.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared._Fish.ObrCall;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.ObrCall;

public sealed partial class ObrCallSystem
{
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private RandomHumanoidSystem _randomHumanoid = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    /// <summary>
    /// Спавнит отряд на мапперских SpawnOnTrigger-точках ЦК (как кнопки), без шаттла.
    /// </summary>
    private bool SpawnObrInCryo(
        ObrTeamPrototype team,
        string mission,
        EntityUid centcommGrid,
        EntityUid actor,
        EntityUid console)
    {
        if (team.CryoRoleSpawners.Count == 0)
        {
            Fail(console, actor, Loc.GetString("obr-call-error-no-cryo-spawns"), purchaseMode: false);
            return false;
        }

        var pointsByRole = CollectCryoSpawnPoints(centcommGrid);
        if (pointsByRole.Count == 0)
        {
            Fail(console, actor, Loc.GetString("obr-call-error-no-cryo-spawns"), purchaseMode: false);
            return false;
        }

        var spawned = new List<EntityUid>();
        foreach (var spawnerProto in team.CryoRoleSpawners)
        {
            var roleKey = TryGetCryoRoleKey(spawnerProto.Id);
            if (roleKey == null)
            {
                _sawmill.Warning($"OBR cryo: cannot resolve role key for {spawnerProto}");
                continue;
            }

            if (!pointsByRole.TryGetValue(roleKey, out var points) || points.Count == 0)
            {
                _sawmill.Warning($"OBR cryo: no free CentComm spawn point for role {roleKey} ({spawnerProto})");
                continue;
            }

            var point = points[0];
            points.RemoveAt(0);

            if (!TrySpawnCryoMember(spawnerProto, Transform(point).Coordinates, mission, out var member))
            {
                _sawmill.Warning($"OBR cryo: failed to spawn {spawnerProto} at {ToPrettyString(point)}");
                continue;
            }

            spawned.Add(member);
        }

        if (spawned.Count == 0)
        {
            Fail(console, actor, Loc.GetString("obr-call-error-no-cryo-spawns"), purchaseMode: false);
            return false;
        }

        var deployment = EntityManager.SpawnEntity(null, new MapCoordinates(default, MapId.Nullspace));
        var cryo = EnsureComp<ObrCryoDeploymentComponent>(deployment);
        cryo.TeamId = team.ID;
        cryo.Mission = mission;
        cryo.SpawnedMembers = spawned;

        _activeCalls[team.ID] = deployment;

        PlayTeamStartAudio(team);

        var teamName = Loc.GetString(team.Name);
        var missionNote = string.IsNullOrWhiteSpace(mission) ? string.Empty : $"; mission: {mission}";
        _adminLog.Add(
            LogType.Action,
            LogImpact.High,
            $"{ToPrettyString(actor)} spawned OBR team {team.ID} in CentComm cryo via {ToPrettyString(console)} ({spawned.Count} members){missionNote}");

        var success = Loc.GetString("obr-call-success-cryo", ("team", teamName));
        if (console.Valid && !Deleted(console))
        {
            _popup.PopupEntity(success, console, actor, PopupType.Medium);
            UpdateUi(console, purchaseMode: false, actor, success);
        }

        foreach (var member in spawned)
        {
            if (!TryComp<ActorComponent>(member, out var actorComp))
                continue;

            ApplyMissionToEntity(member, actorComp.PlayerSession, mission);
        }

        return true;
    }

    private Dictionary<string, List<EntityUid>> CollectCryoSpawnPoints(EntityUid centcommGrid)
    {
        var result = new Dictionary<string, List<EntityUid>>();
        var query = EntityQueryEnumerator<SpawnOnTriggerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spawn, out var xform))
        {
            if (xform.GridUid != centcommGrid)
                continue;

            var roleKey = TryGetCryoRoleKey(spawn.Proto.Id);
            if (roleKey == null)
                continue;

            if (!result.TryGetValue(roleKey, out var list))
            {
                list = new List<EntityUid>();
                result[roleKey] = list;
            }

            list.Add(uid);
        }

        foreach (var list in result.Values)
            list.Sort((a, b) => a.CompareTo(b));

        return result;
    }

    private static string? TryGetCryoRoleKey(string protoId)
    {
        if (protoId.Contains("DeathSquad", StringComparison.OrdinalIgnoreCase))
            return null;

        if (protoId.Contains("CBURN", StringComparison.OrdinalIgnoreCase))
            return "CBURN";

        if (!protoId.Contains("ERT", StringComparison.OrdinalIgnoreCase))
            return null;

        if (protoId.Contains("Leader", StringComparison.OrdinalIgnoreCase))
            return "Leader";
        if (protoId.Contains("Engineer", StringComparison.OrdinalIgnoreCase))
            return "Engineer";
        if (protoId.Contains("Medical", StringComparison.OrdinalIgnoreCase))
            return "Medical";
        if (protoId.Contains("Chaplain", StringComparison.OrdinalIgnoreCase))
            return "Chaplain";
        if (protoId.Contains("Security", StringComparison.OrdinalIgnoreCase))
            return "Security";
        if (protoId.Contains("Janitor", StringComparison.OrdinalIgnoreCase))
            return "Janitor";

        return null;
    }

    private bool TrySpawnCryoMember(
        EntProtoId spawnerProto,
        EntityCoordinates coordinates,
        string mission,
        out EntityUid humanoid)
    {
        humanoid = default;

        if (!_prototypes.TryIndex(spawnerProto, out EntityPrototype? proto))
            return false;

        var spawnerName = _factory.GetComponentName<RandomHumanoidSpawnerComponent>();
        if (!proto.Components.TryGetValue(spawnerName, out var entry))
            return false;

        var spawner = (RandomHumanoidSpawnerComponent)entry.Component;
        if (spawner.SettingsPrototypeId is not { } settingsId)
            return false;

        humanoid = _randomHumanoid.SpawnRandomHumanoid(settingsId, coordinates, proto.Name);

        var target = EnsureComp<ObrMissionTargetComponent>(humanoid);
        target.Mission = mission;
        return true;
    }

    private void PlayTeamStartAudio(ObrTeamPrototype team)
    {
        if (!_prototypes.TryIndex(team.GameRule, out EntityPrototype? ruleProto))
            return;

        var eventName = _factory.GetComponentName<StationEventComponent>();
        if (!ruleProto.Components.TryGetValue(eventName, out var entry))
            return;

        var stationEvent = (StationEventComponent)entry.Component;
        if (stationEvent.StartAudio == null)
            return;

        var filter = Filter.Empty().AddWhere(_gameTicker.UserHasJoinedGame);
        _audio.PlayGlobal(stationEvent.StartAudio, filter, true);
    }
}
