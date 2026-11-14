using Content.Shared.Hands.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Silicons.Borgs.Components;

/// <summary>
/// This is used for a <see cref="BorgModuleComponent"/> that provides items to the entity it's installed into.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedBorgSystem))]
public sealed partial class ItemBorgModuleComponent : Component
{
    /// <summary>
    /// The hands that are provided.
    /// </summary>
    [DataField(required: true)]
    public List<BorgHand> Hands = new();

    /// <summary>
    /// The items stored within the hands.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, EntityUid> StoredItems = new();

    /// <summary>
    /// Whether the provided items have been spawned.
    /// This happens the first time the module is used.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Spawned;

    /// <summary>
    /// An ID for the container where items are stored when not in use.
    /// </summary>
    [DataField]
    public string HoldingContainer = "holding_container";
}

/// <summary>
/// A single hand provided by the module.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public partial record struct BorgHand
{
    /// <summary>
    /// The item to spawn in the hand, if any.
    /// </summary>
    [DataField]
    public EntProtoId? Item;

    // Fish-start
    /// <summary>
    /// The settings for the hand, including a whitelist of items hand can pick up.
    /// Fish: руки, которые выдаёт модуль, по умолчанию функциональные: они идут отдельным
    /// рядом (FunctionalHandContainer), а не серединой между chassis-руками Right/Left.
    /// Явно заданный location в YAML это значение переопределяет.
    /// </summary>
    [DataField]
    public Hand Hand = new(HandLocation.Functional);
    // Fish-end

    /// <summary>
    /// Allows hand to act like normal universal humanoid hand. Pick up and put down items.
    /// </summary>
    [DataField]
    public bool ForceRemovable = false;

    // Fish-start
    /// <summary>
    /// Явный пустой конструктор обязателен: у record struct с параметрическим конструктором
    /// неявный пустой не синтезируется, и десериализатор YAML создаёт значение через initobj
    /// (обнуление без конструктора) — field initializer выше не выполнялся бы, и реальным
    /// дефолтом оставался бы HandLocation.Right вместо Functional.
    /// </summary>
    public BorgHand()
    {
    }

    /// <summary>
    /// Рука модуля: предмет, слот и флаг принудительного изъятия.
    /// </summary>
    // Fish-end
    public BorgHand(EntProtoId? item, Hand hand, bool forceRemovable = false)
    {
        Item = item;
        Hand = hand;
        ForceRemovable = forceRemovable;
    }
}
