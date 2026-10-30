using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Fish.Reactions;

/// <summary>
/// Централизованный статус травмы (контузия, шок, хромота, грудь).
/// Не отдельный HP-пул — поверх Damageable / Bloodstream / Crit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedFishTraumaSystem), typeof(SharedFishCharacterReactionsSystem))]
public sealed partial class FishTraumaStatusComponent : Component
{
    /// <summary>0–1: слабость от кровопотери / шока (обновляется тиком).</summary>
    [DataField, AutoNetworkedField]
    public float ShockFactor;

    /// <summary>0–1: ограничение ног (хромота / неспособность встать).</summary>
    [DataField, AutoNetworkedField]
    public float LegImpairment;

    /// <summary>0–1: тяжесть контузии / ЧМТ.</summary>
    [DataField, AutoNetworkedField]
    public float ConcussionSeverity;

    /// <summary>Момент окончания контузии. <see cref="TimeSpan.Zero"/> — эффекта нет.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan ConcussionUntil;

    /// <summary>Звон / приглушение слуха (игровой суррогат акустической баротравмы).</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan TinnitusUntil;

    /// <summary>Момент окончания дезориентации. <see cref="TimeSpan.Zero"/> — эффекта нет.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan DisorientUntil;

    /// <summary>«Blast lung» / травма груди — затрудняет дыхание и вставание.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan ChestTraumaUntil;

    /// <summary>Тяжесть травмы груди, диапазон 0–1. Действует, пока не истёк <see cref="ChestTraumaUntil"/>.</summary>
    [DataField, AutoNetworkedField]
    public float ChestTraumaSeverity;

    /// <summary>
    /// Следующий серверный тик затухания. Не сетевой: клиент таймер не читает.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextUpdate;

    /// <summary>Последняя сила отброса взрывом (для UI / диагностики).</summary>
    [DataField, AutoNetworkedField]
    public float LastBlastForce;
}
