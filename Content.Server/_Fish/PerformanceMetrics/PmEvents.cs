using Robust.Shared.GameObjects;

namespace Content.Server._Fish.PerformanceMetrics;

/// <summary>
/// Внутреннее серверное уведомление о нанесённом уроне.
/// Нужен потому, что движок разрешает только одну направленную подписку на пару
/// (компонент, событие), а метрикам боя тоже нужны эти же данные.
/// </summary>
public sealed class PmDamageObservedEvent : EntityEventArgs
{
    public EntityUid Victim;

    /// <summary>Тот, от кого пришёл урон: существо или предмет в его руках.</summary>
    public EntityUid? Source;

    public float Total;
}
