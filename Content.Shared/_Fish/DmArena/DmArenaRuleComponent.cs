using Content.Shared.Inventory;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.DmArena;

[DataDefinition]
public sealed partial class DmArenaStoreItem
{
    [DataField(required: true)]
    public ProtoId<StartingGearPrototype> Gear;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    [DataField(required: true)]
    public LocId Category;

    [DataField]
    public LocId? Group;

    [DataField(required: true)]
    public EntProtoId Sprite;

    [DataField(required: true)]
    public int Cost;

    [DataField]
    public bool EquipFirst;

    [DataField]
    public bool InHand;

    [DataField]
    public int MaxCount = 1;

    [DataField]
    public string? AmmoType;

    // "base" доступно всегда
    [DataField]
    public string? AmmoFor;
}

[DataDefinition]
public sealed partial class DmArenaStoreSet
{
    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    [DataField(required: true)]
    public List<ProtoId<StartingGearPrototype>> Items = new();
}

[RegisterComponent]
public sealed partial class DmArenaRuleComponent : Component
{
    public MapId? ArenaMap;

    public HashSet<EntityUid> ArenaGrids = new();

    public HashSet<NetEntity> Players = new();

    public bool Active;

    // Пресет живёт до конца раунда
    public Dictionary<NetUserId, List<int>> Presets = new();

    public TimeSpan NextCleanup;

    public Dictionary<NetUserId, TimeSpan> RespawnCooldowns = new();

    [DataField]
    public ProtoId<StartingGearPrototype> Gear = "FishDmArenaBaseGear";

    [DataField]
    public List<DmArenaStoreItem> ShopItems = new();

    [DataField]
    public List<DmArenaStoreSet> Sets = new();

    [DataField]
    public int Budget = 100;

    [DataField]
    public TimeSpan CleanupInterval = TimeSpan.FromSeconds(25);

    [DataField]
    public float SweepDespawnTime = 10f;

    [DataField]
    public TimeSpan RespawnCooldown = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan CombatLock = TimeSpan.FromSeconds(10);

    [DataField]
    public SlotFlags LockedSlots = SlotFlags.OUTERCLOTHING | SlotFlags.HEAD | SlotFlags.BELT | SlotFlags.BACK | SlotFlags.SUITSTORAGE;
}
