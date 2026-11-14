using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Ipc;

/// <summary>
/// Маркер шасси КПБ (Paradise Machine / IPC) с параметрами энергосистемы.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishIpcComponent : Component
{
    /// <summary>
    /// Текущий уровень заряда 0–10 для алертов и модификаторов скорости.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public short ChargeState = 10;

    /// <summary>
    /// КПБ в режиме отключения из-за разрядки батареи.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool PowerDown;

    /// <summary>
    /// Питание от батарейного слота (расход через DrainPerSecond).
    /// </summary>
    [DataField]
    public bool BatteryPowered = true;

    /// <summary>
    /// Расход Дж/с при активности (Paradise microbattery / EE drain).
    /// </summary>
    [DataField]
    public float DrainPerSecond = 1f;

    /// <summary>Копит секунду, чтобы TryUseCharge не пачкал батарею каждый тик.</summary>
    [ViewVariables]
    public float ChassisDrainTimer;

    [DataField]
    public ProtoId<AlertPrototype> BatteryAlert = "BorgBattery";

    [DataField]
    public ProtoId<AlertPrototype> NoBatteryAlert = "BorgBatteryNone";

    /// <summary>
    /// Пороги заряда (доля 0–1) для предупреждений.
    /// </summary>
    [DataField]
    public float ChargeThresholdMid = 0.8f;

    [DataField]
    public float ChargeThresholdLow = 0.35f;

    [DataField]
    public float ChargeThresholdCritical = 0.1f;

    /// <summary>
    /// Множители скорости по ChargeState (ключ 0–10, как алерт батареи).
    /// </summary>
    [DataField(required: true)]
    public Dictionary<int, float> SpeedModifierThresholds = new()
    {
        [10] = 1f,
        [2] = 1f,
        [1] = 0.65f,
        [0] = 0.2f,
    };

    /// <summary>
    /// Разрешить естественный сон (по умолчанию КПБ не спят).
    /// </summary>
    [DataField]
    public bool AllowSleep;

    /// <summary>
    /// Дробный остаток crit-brute между тиками (FixedPoint2 иначе теряет урон).
    /// </summary>
    [ViewVariables]
    public float CritBruteRemainder;

    /// <summary>
    /// Пока истина, урон наносит сам крит-тик и жужжание от удара его не считает.
    /// </summary>
    [ViewVariables]
    public bool SuppressDamageBuzz;
}
