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
    /// Множитель к базовым 1000 Дж за тик зарядки.
    /// </summary>
    [DataField]
    public float DrinkMultiplier = 5f;

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

    [DataField]
    public SoundSpecifier? DrinkSound = new SoundPathSpecifier("/Audio/Effects/Lightning/lightningshock.ogg");
}
