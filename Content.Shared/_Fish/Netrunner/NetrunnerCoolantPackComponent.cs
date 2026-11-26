using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Netrunner;

/// <summary>Single-use stackable coolant pack that lowers a character's body temperature down to 20 C.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NetrunnerCoolantPackComponent : Component
{
    /// <summary>Target temperature in Kelvin (20 C = 293.15 K).</summary>
    [DataField]
    public float TargetTemperature = 293.15f;

    /// <summary>Continuous application time.</summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);

    [DataField]
    public SoundSpecifier? BeginSound;

    [DataField]
    public SoundSpecifier? EndSound;
}

/// <summary>DoAfter event raised when applying a coolant pack to a character.</summary>
[Serializable, NetSerializable]
public sealed partial class NetrunnerCoolantDoAfterEvent : SimpleDoAfterEvent;
