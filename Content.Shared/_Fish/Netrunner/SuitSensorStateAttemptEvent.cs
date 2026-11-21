namespace Content.Shared.Medical.SuitSensors;

/// <summary>Allows temporary interference to suppress a sensor report without changing its mode.</summary>
[ByRefEvent]
public record struct SuitSensorStateAttemptEvent(bool Cancelled = false);
