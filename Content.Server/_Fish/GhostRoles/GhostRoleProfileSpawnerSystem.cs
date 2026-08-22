using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Station.Systems;
using Content.Shared._Fish.GhostRoles.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.GhostRoles;

/// <summary>
/// Спавнит персонажа игрока из профиля при взятии ghost role через <see cref="GhostRoleProfileSpawnerComponent"/>.
/// </summary>
public sealed class GhostRoleProfileSpawnerSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly GhostRoleSystem _ghostRole = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly StationSystem _stations = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhostRoleProfileSpawnerComponent, TakeGhostRoleEvent>(OnTakeRole);
    }

    private void OnTakeRole(Entity<GhostRoleProfileSpawnerComponent> ent, ref TakeGhostRoleEvent args)
    {
        if (args.TookRole)
            return;

        if (!TryComp(ent, out GhostRoleComponent? ghostRole) || !CanTakeGhost(ent, ghostRole))
        {
            args.TookRole = false;
            return;
        }

        var profile = _gameTicker.GetPlayerProfile(args.Player);
        var coords = Transform(ent).Coordinates;
        var station = _stations.GetOwningStation(ent);

        EntityUid mob;
        if (ent.Comp.Prototype is { } customProto)
        {
            if (profile is HumanoidCharacterProfile humanoidProfile)
            {
                profile = humanoidProfile.WithSpecies("Human");
            }
            mob = Spawn(customProto, coords);
            _stationSpawning.SpawnPlayerMob(coords, null, profile, station, mob);
        }
        else
        {
            mob = _stationSpawning.SpawnPlayerMob(coords, null, profile, station);
        }

        // FIsh: экипировка через startingGear (Sunrise), а не Loadout MapInit на мобе
        if (ent.Comp.StartingGear is { } gearId &&
            _prototypes.TryIndex(gearId, out StartingGearPrototype? gear))
        {
            _stationSpawning.EquipStartingGear(mob, gear, raiseEvent: true);
        }

        _transform.AttachToGridOrMap(mob);

        // Prepend role/job name to entity name (e.g. "Строитель Имя Фамилия (уникальный номер)")
        var roleName = Loc.GetString(ghostRole.RoleName);
        var oldName = Name(mob);
        var newName = $"{roleName} {oldName}";
        _metaData.SetEntityName(mob, newName);

        if (ent.Comp.Factions.Count > 0)
            _npcFaction.AddFactions((mob, null), ent.Comp.Factions);

        RaiseLocalEvent(mob, new GhostRoleSpawnerUsedEvent(ent, mob));

        if (ghostRole.MakeSentient)
            _mindSystem.MakeSentient(mob, ghostRole.AllowMovement, ghostRole.AllowSpeech);

        EnsureComp<MindContainerComponent>(mob);

        _ghostRole.GhostRoleInternalCreateMindAndTransfer(args.Player, ent, mob, ghostRole);
        _ghostRole.UnregisterGhostRole((ent, ghostRole));
        QueueDel(ent);

        args.TookRole = true;
    }

    private bool CanTakeGhost(EntityUid uid, GhostRoleComponent component)
    {
        return !component.Taken && !MetaData(uid).EntityPaused;
    }
}
