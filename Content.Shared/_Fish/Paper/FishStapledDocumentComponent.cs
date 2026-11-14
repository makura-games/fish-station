using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Paper;

/// <summary>
/// Многостраничный пакет документов. Сама сущность — первая (внешняя) страница;
/// дополнительные страницы лежат в контейнере как отдельные сущности с <c>PaperComponent</c>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishStapledDocumentComponent : Component
{
    public const string ContainerId = "fish-stapled-pages";

    /// <summary>
    /// Контейнер дополнительных страниц (после внешней).
    /// </summary>
    [ViewVariables]
    public Container Pages = default!;

    /// <summary>
    /// Индекс текущей страницы для UI (0 = эта сущность).
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CurrentPage;

    /// <summary>
    /// Технический потолок страниц (внешняя + дополнительные), защита от злоупотреблений.
    /// </summary>
    [DataField]
    public int MaxPages = 50;
}

[Serializable, NetSerializable]
public sealed class FishPaperPageTurnMessage(int delta) : BoundUserInterfaceMessage
{
    public int Delta { get; } = delta;
}

[Serializable, NetSerializable]
public sealed class FishPaperRemoveCurrentPageMessage : BoundUserInterfaceMessage;
