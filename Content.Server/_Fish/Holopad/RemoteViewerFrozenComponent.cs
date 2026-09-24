using Robust.Shared.GameStates;

namespace Content.Server.Holopad;

/// <summary>
/// Пока этот компонент на сущности, её <c>InputMoverComponent.CanMove</c> удерживается в false.
/// Используется для блокировки тела во время удалённого просмотра.
/// </summary>
[RegisterComponent]
public sealed partial class RemoteViewerFrozenComponent : Component
{
}