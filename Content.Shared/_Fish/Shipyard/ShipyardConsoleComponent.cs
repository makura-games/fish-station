using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Shipyard;

/// <summary>
/// Консоль верфи: покупка шаттлов за средства счёта станции.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ShipyardConsoleComponent : Component
{
    /// <summary>
    /// Счёт станции, с которого списывается оплата.
    /// </summary>
    [DataField]
    public ProtoId<CargoAccountPrototype> Account = "Cargo";

    /// <summary>
    /// Какая группа VesselPrototype показывается на этой консоли.
    /// </summary>
    [DataField]
    public string VesselGroup = "civilian";

    /// <summary>
    /// Тег PriorityDock по умолчанию, если у сосуда свой тег не задан.
    /// </summary>
    [DataField]
    public string? DefaultPriorityDockTag = "DockPersonal";

    /// <summary>
    /// Звук успешной покупки и стыковки шаттла.
    /// </summary>
    [DataField]
    public SoundSpecifier ApproveSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    /// <summary>
    /// Звук отказа в покупке (нет доступа, средств, дока и т.п.).
    /// </summary>
    [DataField]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    /// <summary>
    /// Блокировка повторной покупки на время обработки.
    /// </summary>
    [ViewVariables]
    public bool Purchasing;
}
