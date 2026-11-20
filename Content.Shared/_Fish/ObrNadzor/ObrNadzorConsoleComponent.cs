using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.ObrNadzor;

/// <summary>
/// Консоль надзора ОСО: выбор маяка и сессионный удалённый контроль глазом (как abductor cams).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ObrNadzorConsoleComponent : Component
{
    [DataField]
    public EntProtoId RemoteEntityProto = "FishObrNadzorConsoleEye";

    [DataField]
    public TimeSpan BlackoutDuration = TimeSpan.FromSeconds(20);

    [DataField]
    public SoundPathSpecifier AnnouncementSound = new("/Audio/_Fish/Announcements/nadzor.ogg");

    [DataField]
    public Color AnnouncementColor = Color.FromHex("#c22532");

    /// <summary>
    /// Прототипы станций, доступных для надзора (как у abductor console).
    /// </summary>
    [DataField]
    public HashSet<EntProtoId> StationWhitelist = new() { "StandardNanotrasenStation" };

    [DataField, AutoNetworkedField]
    public NetEntity? RemoteEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? Opener;

    /// <summary>
    /// Оператор активной сессии удалённого контроля (тело у консоли).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? OperatorBody;

    /// <summary>
    /// Выбранный маяк (цель для спавна глаза при takeover).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? TargetBeacon;

    /// <summary>
    /// Станция, выбранная для наблюдения / контроля.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? TargetStation;
}

/// <summary>
/// Маркер активной сессии надзора на операторе: связь с консолью и action снятия контроля.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ObrNadzorSessionComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Console;

    [DataField, AutoNetworkedField]
    public EntityUid? ReleaseAction;

    /// <summary>
    /// Защита от рекурсии при штатном завершении сессии.
    /// </summary>
    [ViewVariables]
    public bool Ending;
}

/// <summary>
/// Маркер глаза надзора — при удалении снимает сессию с консоли.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ObrNadzorEyeComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Console;
}
