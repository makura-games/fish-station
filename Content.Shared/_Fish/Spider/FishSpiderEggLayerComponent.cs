using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Spider;

/// <summary>
/// Позволяет пауку откладывать прикреплённые яйца на паутине за счёт голода.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(FishSpiderEggLayerSystem))]
public sealed partial class FishSpiderEggLayerComponent : Component
{
    /// <summary>
    /// Прототип действия для откладывания яйца.
    /// </summary>
    [DataField]
    public EntProtoId Action = "FishActionSpiderLayEgg";

    /// <summary>
    /// Сущность выданного действия.
    /// </summary>
    [AutoNetworkedField]
    public EntityUid? ActionEntity;

    /// <summary>
    /// Прототип создаваемого паучьего яйца.
    /// </summary>
    [DataField]
    public EntProtoId EggPrototype = "FishEggGiantSpiderAngry";

    /// <summary>
    /// Количество голода, списываемое при создании одного яйца.
    /// Допускается частичная оплата: откладывание разрешено при сытости не ниже <see cref="MinHungerRatio"/>,
    /// даже если текущий голод меньше <see cref="HungerCost"/> (итоговое значение ограничивается нижней границей <c>SatiationSystem</c>).
    /// </summary>
    [DataField]
    public float HungerCost = 40f;

    /// <summary>
    /// Минимальная доля сытости (от 0 до 1), ниже которой невозможно отложить яйцо.
    /// Проверяется именно текущий уровень сытости без предварительного вычитания <see cref="HungerCost"/>.
    /// </summary>
    [DataField]
    public float MinHungerRatio = 0.2f;

    /// <summary>
    /// Длительность процесса откладывания яйца.
    /// </summary>
    [DataField]
    public TimeSpan LayDelay = TimeSpan.FromSeconds(3);
}

/// <summary>
/// Событие активации действия откладывания паучьего яйца.
/// </summary>
public sealed partial class FishSpiderLayEggActionEvent : InstantActionEvent;

/// <summary>
/// Событие завершения DoAfter при откладывании паучьего яйца.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class FishSpiderLayEggDoAfterEvent : SimpleDoAfterEvent;
