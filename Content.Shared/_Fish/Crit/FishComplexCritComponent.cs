using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Fish.Crit;

/// <summary>
/// Включает поведение критического состояния по правилам расширенного Crit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedFishComplexCritSystem), typeof(Content.Shared._Fish.Reactions.SharedFishCharacterReactionsSystem))]
public sealed partial class FishComplexCritComponent : Component
{
    /// <summary>
    /// Порог урона для входа в Critical (обычно 100).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CritDamageThreshold = 100f;

    /// <summary>
    /// Порог урона для смерти (обычно ~200). Не поднимаем выше — конфликт с Gib@Blunt400.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DeadDamageThreshold = 200f;

    /// <summary>
    /// Интервал тика осложнений (~2 секунды).
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(2);

    /// <summary>Серверный таймер следующего тика осложнений. Клиент его не читает.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextTick;

    /// <summary>
    /// Исходный порог Death (кэш с MobThresholds; Dead не поднимаем из‑за Gib@400).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float? OriginalDeadThreshold;
}

/// <summary>
/// Маркер полной бессознательности от crit-осложнений, блокирует soft-crit действия.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishCritUnconsciousComponent : Component;

/// <summary>
/// Шок — осложнение критического состояния (3 стадии).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedFishComplexCritSystem), typeof(Content.Shared._Fish.Reactions.SharedFishCharacterReactionsSystem))]
public sealed partial class FishCritShockComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Stage = 1;

    [DataField, AutoNetworkedField]
    public int MaxStages = 3;

    /// <summary>Шанс прогресса стадии за тик.</summary>
    [DataField, AutoNetworkedField]
    public float StageProb = 0.06f;

    /// <summary>Шанс самоизлечения за тик.</summary>
    [DataField, AutoNetworkedField]
    public float CureProb = 0.10f;
}

/// <summary>
/// Сердечная недостаточность — осложнение критического состояния (3 стадии).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedFishComplexCritSystem), typeof(Content.Shared._Fish.Reactions.SharedFishCharacterReactionsSystem))]
public sealed partial class FishHeartFailureComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Stage = 1;

    [DataField, AutoNetworkedField]
    public int MaxStages = 3;

    /// <summary>Шанс прогресса стадии за тик.</summary>
    [DataField, AutoNetworkedField]
    public float StageProb = 0.05f;

    /// <summary>Шанс самоизлечения за тик.</summary>
    [DataField, AutoNetworkedField]
    public float CureProb = 0.10f;
}

/// <summary>
/// Остановка сердца.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedFishComplexCritSystem), typeof(Content.Shared._Fish.Reactions.SharedFishCharacterReactionsSystem))]
public sealed partial class FishCardiacArrestComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active = true;
}

/// <summary>
/// Потеря дыхания (Remaining в секундах).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedFishComplexCritSystem), typeof(Content.Shared._Fish.Reactions.SharedFishCharacterReactionsSystem))]
public sealed partial class FishLoseBreathComponent : Component
{
    /// <summary>Серверный таймер оставшегося LoseBreath (не сетевой каждый кадр).</summary>
    [DataField]
    public TimeSpan Remaining;

    [DataField, AutoNetworkedField]
    public TimeSpan MaxRemaining = TimeSpan.FromSeconds(50);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextOxyTick;
}
