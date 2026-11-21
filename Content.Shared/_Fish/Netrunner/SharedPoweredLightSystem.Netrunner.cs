namespace Content.Shared.Light.EntitySystems;

public abstract partial class SharedPoweredLightSystem
{
    /// <summary>Recalculates output after a temporary operating restriction changes.</summary>
    public void RefreshLight(EntityUid uid) => UpdateLight(uid);
}

/// <summary>Queries transient restrictions after reading the light's configured switch state.</summary>
[ByRefEvent]
public record struct GetPoweredLightEnabledEvent(bool Enabled);
