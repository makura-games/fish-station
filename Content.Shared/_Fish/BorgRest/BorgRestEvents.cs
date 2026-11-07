using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.BorgRest;

/// <summary>
/// Fish: действие отдыха борга — мгновенное, запускает DoAfter переключения.
/// </summary>
public sealed partial class BorgRestActionEvent : InstantActionEvent
{
}

/// <summary>
/// Fish: DoAfter входа/выхода из отдыха (прерывается движением или уроном).
/// [Serializable, NetSerializable] обязателен: DoAfter хранит событие в состоянии
/// и сериализует его клиенту (без атрибутов SharedDoAfterSystem падает ассертом).
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BorgRestDoAfterEvent : SimpleDoAfterEvent
{
}

/// <summary>
/// Fish: данные appearance состояния отдыха борга — источник данных
/// для клиентского визуала (Content.Client._Fish).
/// </summary>
[Serializable, NetSerializable]
public enum BorgRestVisuals : byte
{
    /// <summary>
    /// Борг отдыхает.
    /// </summary>
    Resting,
}
