using Content.Shared.NPC.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.GhostRoles.Components;

/// <summary>
/// Маркер ghost role, который спавнит собственного персонажа игрока из профиля (1 слот).
/// </summary>
[RegisterComponent, EntityCategory("Spawner")]
public sealed partial class GhostRoleProfileSpawnerComponent : Component
{
    /// <summary>
    /// Фракции, назначаемые заспавненному персонажу. Пустой список — без изменений.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<NpcFactionPrototype>> Factions = [];

    /// <summary>
    /// Прототип сущности, которую нужно заспавнить вместо стандартного гуманоида из расы в профиле.
    /// </summary>
    [DataField]
    public string? Prototype;

    /// <summary>
    /// Стартовая экипировка роли (Sunrise startingGear). Если задана — выдаётся вместо Loadout на мобе.
    /// </summary>
    [DataField]
    public ProtoId<StartingGearPrototype>? StartingGear;
}
