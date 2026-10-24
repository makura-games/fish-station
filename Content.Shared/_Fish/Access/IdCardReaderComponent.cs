using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Access;

/// <summary>
/// Хранит настройки настенного считывателя ID-карт.
/// </summary>
[RegisterComponent]
public sealed partial class IdCardReaderComponent : Component
{
    /// <summary>
    /// Идентификатор слота для вставляемой ID-карты.
    /// </summary>
    public const string CardSlotId = "IdCardReader-card";

    /// <summary>
    /// Порт, вызываемый после вставки ID-карты.
    /// </summary>
    [DataField]
    public ProtoId<SourcePortPrototype> CardInsertedPort = "FishIdCardInserted";

    /// <summary>
    /// Порт, вызываемый после извлечения ID-карты.
    /// </summary>
    [DataField]
    public ProtoId<SourcePortPrototype> CardRemovedPort = "FishIdCardRemoved";

    /// <summary>
    /// Порт, передающий высокий уровень, пока во вставленной ID-карте есть требуемый доступ.
    /// </summary>
    [DataField]
    public ProtoId<SourcePortPrototype> AccessGrantedPort = "FishIdCardAccessGranted";

    /// <summary>
    /// Порт, передающий высокий уровень, пока собственная вставленная карта имеет требуемый доступ.
    /// </summary>
    [DataField]
    public ProtoId<SourcePortPrototype> LocalAuthorizationPort = "FishIdCardLocalAuthorization";

    /// <summary>
    /// Порт, вызываемый для закрытия подключённого затвора.
    /// </summary>
    [DataField]
    public ProtoId<SourcePortPrototype> CloseRequestPort = "FishIdCardReaderCloseRequest";

    /// <summary>
    /// Порт, принимающий авторизацию от другого считывателя.
    /// </summary>
    [DataField]
    public ProtoId<SinkPortPrototype> AuthorizationPort = "FishIdCardReaderAuthorization";

    /// <summary>
    /// Порт обратной связи о состоянии подключённого затвора.
    /// </summary>
    [DataField]
    public ProtoId<SinkPortPrototype> DoorStatusPort = "FishIdCardReaderDoorStatus";

    /// <summary>
    /// Количество одновременно подтверждённых считывателей, необходимое для открытия.
    /// </summary>
    [DataField]
    public int RequiredAuthorizations = 1;

    /// <summary>
    /// Задержка перед первой попыткой закрыть затвор после открытия.
    /// </summary>
    [DataField]
    public TimeSpan CloseDelay = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Задержка между повторными попытками закрыть незакрывшийся затвор.
    /// </summary>
    [DataField]
    public TimeSpan CloseRetryDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Указывает, что карта в собственном слоте прошла проверку доступа.
    /// </summary>
    public bool LocalAuthorization;

    /// <summary>
    /// Считыватели, от которых сейчас получен высокий уровень авторизации.
    /// </summary>
    public HashSet<EntityUid> RemoteAuthorizations = [];

    /// <summary>
    /// Подключённые затворы, сообщающие, что они не закрыты.
    /// </summary>
    public HashSet<EntityUid> OpenDoors = [];

    /// <summary>
    /// Последнее переданное состояние итоговой авторизации.
    /// </summary>
    public bool AuthorizationOutput;
}
