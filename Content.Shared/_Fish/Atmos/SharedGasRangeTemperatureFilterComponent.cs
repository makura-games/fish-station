using Robust.Shared.Serialization;

namespace Content.Shared.Atmos.Piping.Trinary.Components;

[Serializable, NetSerializable]
public enum GasRangeTemperatureFilterUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class GasRangeTemperatureFilterBoundUserInterfaceState : BoundUserInterfaceState
{
    public string FilterLabel { get; }
    public float TransferRate { get; }
    public bool Enabled { get; }
    public float LowTemperature { get; }
    public float HighTemperature { get; }

    public GasRangeTemperatureFilterBoundUserInterfaceState(
        string filterLabel,
        float transferRate,
        bool enabled,
        float lowTemperature,
        float highTemperature)
    {
        FilterLabel = filterLabel;
        TransferRate = transferRate;
        Enabled = enabled;
        LowTemperature = lowTemperature;
        HighTemperature = highTemperature;
    }
}

[Serializable, NetSerializable]
public sealed class GasRangeTemperatureFilterToggleStatusMessage : BoundUserInterfaceMessage
{
    public bool Enabled { get; }
    public GasRangeTemperatureFilterToggleStatusMessage(bool enabled) => Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class GasRangeTemperatureFilterChangeRateMessage : BoundUserInterfaceMessage
{
    public float Rate { get; }
    public GasRangeTemperatureFilterChangeRateMessage(float rate) => Rate = rate;
}

[Serializable, NetSerializable]
public sealed class GasRangeTemperatureFilterChangeRangeMessage : BoundUserInterfaceMessage
{
    public float Low { get; }
    public float High { get; }

    public GasRangeTemperatureFilterChangeRangeMessage(float low, float high)
    {
        Low = low;
        High = high;
    }
}