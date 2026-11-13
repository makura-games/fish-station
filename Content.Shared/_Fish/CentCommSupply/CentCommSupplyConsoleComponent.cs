using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.CentCommSupply;

/// <summary>
/// A console that orders Central Command supplies for a selected station.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CentCommSupplyConsoleComponent : Component
{
    /// <summary>
    /// Maximum number of crates one order may occupy.
    /// </summary>
    [DataField]
    public int MaxCratesPerOrder = 6;

    /// <summary>
    /// Maximum number of distinct products in one order.
    /// </summary>
    [DataField]
    public int MaxProductsPerOrder = 30;

    /// <summary>
    /// Delay between successful orders.
    /// </summary>
    [DataField]
    public TimeSpan OrderCooldown = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Crate prototype used to pack ordered items.
    /// </summary>
    [DataField]
    public EntProtoId CratePrototype = "FishCrateCentCommSupply";

    /// <summary>
    /// Delay between accepting an order and delivering its crates to the station.
    /// </summary>
    [DataField]
    public TimeSpan DeliveryDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long an order may wait for free cargo pallets after the delivery delay expires.
    /// </summary>
    [DataField]
    public TimeSpan DeliveryTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Localized announcement played when an order is accepted. Argument: station name.
    /// </summary>
    [DataField]
    public LocId OrderAnnouncement = "centcomm-supply-announcement";

    /// <summary>
    /// Localized sender shown in the order announcement.
    /// </summary>
    [DataField]
    public LocId AnnouncementSender = "centcomm-supply-announcement-sender";

    /// <summary>
    /// Sound played globally when the order arrives at the station.
    /// </summary>
    [DataField]
    public SoundSpecifier? DeliverySound = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");

    /// <summary>
    /// Sound played when the order is approved.
    /// </summary>
    [DataField]
    public SoundSpecifier? ApproveSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    /// <summary>
    /// Состояние консоли для каждого игрока с открытым интерфейсом.
    /// </summary>
    [NonSerialized]
    [ViewVariables]
    public readonly Dictionary<EntityUid, CentCommSupplyUserStateMessage> UserStates = new();

    /// <summary>
    /// Earliest time the next order can be placed.
    /// </summary>
    [NonSerialized]
    [ViewVariables]
    public TimeSpan NextOrderTime;
}
