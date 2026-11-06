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
        foreach (var entry in team.CryoRoleSpawners)
        {
            if (!pointsByRole.TryGetValue(entry.RoleKey, out var points) || points.Count == 0)
            {
                _sawmill.Warning($"OBR cryo: no free CentComm spawn point for role {entry.RoleKey} ({entry.Spawner})");
                continue;
            }

            var point = points[0];
            points.RemoveAt(0);

            if (!TrySpawnCryoMember(entry.Spawner, Transform(point).Coordinates, mission, out var member))
            {
                _sawmill.Warning($"OBR cryo: failed to spawn {entry.Spawner} at {ToPrettyString(point)}");
                continue;
            }

            spawned.Add(member);
        }

        if (spawned.Count == 0)
        {
            Fail(console, actor, Loc.GetString("obr-call-error-no-cryo-spawns"), purchaseMode: false);
            return false;
        }

        var deployment = Spawn(null, new MapCoordinates(default, MapId.Nullspace));
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

        // Миссия на ghost-role подтянется через ObrMissionTarget + MindAddedMessage.
        return true;
    }

    private Dictionary<string, List<EntityUid>> CollectCryoSpawnPoints(EntityUid centcommGrid)
    {
        var roleBySpawner = BuildCryoRoleLookup();
        var result = new Dictionary<string, List<EntityUid>>();
        var query = EntityQueryEnumerator<SpawnOnTriggerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spawn, out var xform))
        {
            if (xform.GridUid != centcommGrid)
                continue;

            if (!roleBySpawner.TryGetValue(spawn.Proto.Id, out var roleKey))
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

    /// <summary>
    /// Строит protoId спавнера → RoleKey из всех obrTeam (единственный источник правды).
    /// </summary>
    private Dictionary<string, string> BuildCryoRoleLookup()
    {
        var map = new Dictionary<string, string>();
        foreach (var team in _prototypes.EnumeratePrototypes<ObrTeamPrototype>())
        {
            foreach (var entry in team.CryoRoleSpawners)
            {
                var id = entry.Spawner.Id;
                if (map.TryGetValue(id, out var existing) && existing != entry.RoleKey)
                {
                    _sawmill.Warning($"OBR cryo: spawner {id} mapped to both {existing} and {entry.RoleKey}");
                    continue;
                }

                map[id] = entry.RoleKey;
            }
        }

        return map;
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
