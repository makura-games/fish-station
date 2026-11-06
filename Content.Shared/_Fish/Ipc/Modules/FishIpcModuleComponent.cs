using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Ipc.Modules;

/// <summary>
/// Модуль апгрейда шасси КПБ. Устанавливается в <see cref="FishIpcModuleHostComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedFishIpcModuleSystem))]
public sealed partial class FishIpcModuleComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? InstalledEntity;

    [ViewVariables]
    public bool Installed => InstalledEntity != null;

    /// <summary>Пассивный расход Дж/с с батареи хоста, пока модуль установлен.</summary>
    [DataField]
    public float PassiveDrainPerSecond;

    /// <summary>
    /// Группа взаимоисключения (например nano / magnetic). Пусто = без ограничений.
    /// </summary>
    [DataField]
    public string? ExclusionGroup;

    /// <summary>Ниже этой доли заряда пассивные эффекты (хил и т.п.) останавливаются.</summary>
    [DataField]
    public float MinChargeFraction = 0.05f;
}

/// <summary>Поднято на модуле при установке.</summary>
[ByRefEvent]
public readonly record struct FishIpcModuleInstalledEvent(EntityUid Host);

/// <summary>Поднято на модуле при снятии.</summary>
[ByRefEvent]
public readonly record struct FishIpcModuleUninstalledEvent(EntityUid Host);

/// <summary>Попытка вставить модуль; можно отменить.</summary>
[ByRefEvent]
public record struct FishIpcModuleInsertAttemptEvent(EntityUid Module, EntityUid Host, bool Cancelled = false);

/// <summary>
/// Попытка ампутации конечности на теле. Поднимается на Body до LimbSystem.Amputate.
/// Модули суставов КПБ могут отменить.
/// </summary>
[ByRefEvent]
public record struct FishIpcAmputationAttemptEvent(EntityUid User, EntityUid Part, bool Cancelled = false);
