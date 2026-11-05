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
    public int MaxProductsPerOrder = 8;

    /// <summary>
    /// Delay between successful orders.
    /// </summary>
    [DataField]
    public TimeSpan OrderCooldown = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Crate prototype used to pack ordered items.
    /// </summary>
    [DataField]
    public EntProtoId CratePrototype = "CrateCommandSecure";

    /// <summary>
    /// Delay between accepting an order and delivering its crates to the station.
    /// </summary>
    [DataField]
    public TimeSpan DeliveryDelay = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Localized announcement played when an order is accepted. Arguments: order number and station name.
    /// </summary>
    [DataField]
    public LocId OrderAnnouncement = "centcomm-supply-announcement";

    /// <summary>
    /// Localized sender shown in the order announcement.
    /// </summary>
    [DataField]
    public LocId AnnouncementSender = "centcomm-supply-announcement-sender";

    /// <summary>
    /// Sound played together with the order announcement.
    /// </summary>
    [DataField]
    public SoundSpecifier? AnnouncementSound = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");

    /// <summary>
    /// Station currently selected in the console.
    /// </summary>
    [ViewVariables]
    public EntityUid? SelectedStation;

    /// <summary>
    /// Earliest time the next order can be placed.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextOrderTime;
}
