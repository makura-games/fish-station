using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Ipc;

/// <summary>
/// Событие обновления уровня заряда КПБ (0–10).
/// </summary>
[Serializable, NetSerializable]
public sealed class FishIpcChargeStateUpdateEvent(short chargePercent) : EntityEventArgs
{
    public short ChargePercent { get; } = chargePercent;
}
