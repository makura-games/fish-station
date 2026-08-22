using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Mechs;

/// <summary>
/// Внутренние отказы шасси Fish Mech (флаги ECS-компонента).
/// </summary>
[Flags, Serializable, NetSerializable]
public enum MechInternalDamageFlags : byte
{
    None = 0,
    /// <summary>Пожар в отсеках.</summary>
    CabinFire = 1 << 0,
    /// <summary>Отказ охлаждения / терморегуляции.</summary>
    CoolantFail = 1 << 1,
    /// <summary>Скачки по силовой шине, жрёт энергию.</summary>
    PowerSpike = 1 << 2,
    /// <summary>Заклинивание привода — рысканье при движении.</summary>
    DriveFault = 1 << 3,
    /// <summary>Пробоина гермокорпуса.</summary>
    HullBreach = 1 << 4,
}
