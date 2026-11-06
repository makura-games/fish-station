using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Ipc;

/// <summary>
/// Позволяет КПБ питать батарею от источников с <see cref="FishBatteryDrinkerSourceComponent"/> (ЛКП / APC).
/// Соответствует Paradise power_cord.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishBatteryDrinkerComponent : Component
{
    [DataField]
    public float DrinkSpeed = 1f;

    /// <summary>
    /// Доля максимального заряда шасси за один цикл подсоса с ЛКП (0.05 = 5%).
    /// </summary>
    [DataField]
    public float DrinkPercent = 0.05f;

    /// <summary>
    /// Устарело: раньше множитель к 1000 Дж. Оставлено для совместимости YAML/адаптера.
    /// </summary>
    [DataField]
    public float DrinkMultiplier = 1f;

    /// <summary>
    /// Доля заряда КПБ, при которой и выше подсос с ЛКП запрещён (1.0 = полностью заряжен).
    /// </summary>
    [DataField]
    public float MaxSelfChargeFraction = 1f;

    [DataField]
    public bool DrinkAll;
}

/// <summary>
/// Источник энергии для зарядки КПБ (обычно ЛКП).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishBatteryDrinkerSourceComponent : Component
{
    [DataField]
    public float DrinkSpeedMulti = 1f;

    /// <summary>
    /// Максимум Дж за один цикл do-after; 0 = без лимита.
    /// </summary>
    [DataField]
    public float MaxAmount;

    /// <summary>
    /// Тот же набор искр, что у ниндзи при сливе питания с подстанции.
    /// </summary>
    [DataField]
    public SoundSpecifier? DrinkSound = new SoundCollectionSpecifier("sparks");
}
