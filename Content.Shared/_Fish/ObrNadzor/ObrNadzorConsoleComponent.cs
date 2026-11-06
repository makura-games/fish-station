using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.ObrNadzor;

/// <summary>
/// Консоль надзора ОСО: наблюдение станции и сессионный удалённый AI-контроль.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ObrNadzorConsoleComponent : Component
{
    [DataField]
    public EntProtoId RemoteEntityProto = "FishObrNadzorConsoleEye";

    [DataField]
    public EntProtoId AiCoreProto = "FishObrNadzorAiCore";

    [DataField]
    public EntProtoId AiBrainProto = "FishObrNadzorAiBrain";

    [DataField]
    public TimeSpan BlackoutDuration = TimeSpan.FromSeconds(20);

    [DataField]
    public SoundPathSpecifier AnnouncementSound = new("/Audio/_Fish/Announcements/nadzor.ogg");

    [DataField, AutoNetworkedField]
    public NetEntity? RemoteEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? Opener;

    /// <summary>
    /// Тело оператора, оставленное у консоли на время AI-сессии.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? OperatorBody;

    /// <summary>
    /// Мозг / held-entity текущей AI-сессии.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SessionBrain;

    /// <summary>
    /// Ядро AI текущей сессии.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SessionCore;

    /// <summary>
    /// Станция, выбранная для наблюдения / контроля.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? TargetStation;
}

/// <summary>
/// Маркер активной сессии надзора на AI-brain: связь с телом и консолью.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ObrNadzorSessionComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Console;

    [DataField, AutoNetworkedField]
    public EntityUid Body;

    [DataField, AutoNetworkedField]
    public EntityUid Core;

    /// <summary>
    /// Защита от рекурсии при штатном завершении сессии.
    /// </summary>
    [ViewVariables]
    public bool Ending;
}
