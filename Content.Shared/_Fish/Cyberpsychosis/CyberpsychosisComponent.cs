using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Cyberpsychosis;

/// <summary>Tracks a character's experimental cyberpsychosis level (0-100%) and passive decay.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberpsychosisComponent : Component
{
    /// <summary>Current cyberpsychosis level in percent (0 to 100).</summary>
    [DataField, AutoNetworkedField]
    public float Level;

    /// <summary>Passive decay rate in percent per second (default: 5% per minute).</summary>
    [DataField]
    public float DecayPerSecond = 5f / 60f;
}

/// <summary>Reduces incoming cyberpsychosis buildup when worn in an inventory slot.</summary>
[RegisterComponent]
public sealed partial class CyberpsychosisResistanceComponent : Component
{
    /// <summary>Multiplier applied to incoming positive cyberpsychosis buildup (e.g. 0.6 = 40% resistance).</summary>
    [DataField]
    public float DamageCoefficient = 0.6f;
}

/// <summary>Debug wand that adds or removes cyberpsychosis on a target.</summary>
[RegisterComponent]
public sealed partial class CyberpsychosisWandComponent : Component
{
    /// <summary>Amount of cyberpsychosis percent to add (positive) or remove (negative).</summary>
    [DataField(required: true)]
    public float Delta;
}
