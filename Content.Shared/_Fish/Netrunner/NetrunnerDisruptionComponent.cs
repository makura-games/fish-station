using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Netrunner;

/// <summary>Temporary suppression that preserves the device's normal settings.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class NetrunnerDisruptionComponent : Component
{
    [AutoNetworkedField] public NetrunnerHackKind Kind;
    [AutoNetworkedField, AutoPausedField] public TimeSpan EndsAt;
    /// <summary>Body whose vision or movement must be refreshed when this effect changes.</summary>
    [AutoNetworkedField] public EntityUid? AffectedUser;
    /// <summary>Movement multiplier supplied by the motor interference program.</summary>
    [AutoNetworkedField] public float MovementMultiplier = 1;
}

/// <summary>Notifies server device adapters after the shared effect has shut down.</summary>
[ByRefEvent]
public record struct NetrunnerDisruptionEndedEvent;
