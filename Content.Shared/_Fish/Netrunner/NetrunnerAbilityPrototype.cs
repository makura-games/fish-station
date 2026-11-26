using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Localization;

namespace Content.Shared._Fish.Netrunner;

/// <summary>Defines the resource costs and execution rules of a netrunner program.</summary>
[Prototype]
public sealed partial class NetrunnerAbilityPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField] public string Suffix = "Fish";
    [DataField(required: true)] public NetrunnerHackKind Kind;
    [DataField(required: true)] public EntProtoId Action;
    [DataField] public float RamCost = 4;
    [DataField] public float TraceCost = 12;
    [DataField] public TimeSpan CastTime = TimeSpan.FromSeconds(2);
    [DataField] public TimeSpan Duration = TimeSpan.FromSeconds(20);
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(15);
    [DataField] public float Range = 6;
    [DataField] public bool AllowOverload = true;
    /// <summary>Permanent unlock price in neural links.</summary>
    [DataField] public int LinkCost = 3;
    /// <summary>Minimum activation time after all purchased accelerators.</summary>
    [DataField] public TimeSpan MinCastTime = TimeSpan.FromSeconds(0.5);
    /// <summary>Damage applied by a short circuit, subject to normal resistances.</summary>
    [DataField] public DamageSpecifier Damage = new();
    /// <summary>Movement multiplier while motor interference is active.</summary>
    [DataField] public float MovementMultiplier = 0.65f;
    /// <summary>Radius of a world-targeted light blackout.</summary>
    [DataField] public float AreaRadius;
    /// <summary>Stamina damage caused by neural static.</summary>
    [DataField] public float StaminaDamage = 30;
    /// <summary>Temperature increase before normal heat resistance.</summary>
    [DataField] public float TemperatureIncrease = 6;
    /// <summary>Fraction of battery capacity drained by one activation.</summary>
    [DataField] public float ChargeFraction = 0.1f;
}

/// <summary>Supported program effects. Execution is dispatched through local events.</summary>
[Serializable, NetSerializable]
public enum NetrunnerHackKind : byte
{
    OpenAirlock,
    DisableCamera,
    DisableLight,
    DisableSensors,
    DisableHeadset,
    NeuralOverload,
    ShortCircuit,
    MotorInterference,
    OpticalGlitch,
    LockWeapon,
    GripShock,
    NeuralStatic,
    ThermalSurge,
    BatteryDrain,
    LockAirlock,
    EraseTrace,
}

public static class NetrunnerProgramNames
{
    public static LocId GetLocId(this NetrunnerHackKind kind) => kind switch
    {
        NetrunnerHackKind.OpenAirlock => "fish-netrunner-program-open-airlock",
        NetrunnerHackKind.DisableCamera => "fish-netrunner-program-disable-camera",
        NetrunnerHackKind.DisableLight => "fish-netrunner-program-disable-light",
        NetrunnerHackKind.DisableSensors => "fish-netrunner-program-disable-sensors",
        NetrunnerHackKind.DisableHeadset => "fish-netrunner-program-disable-headset",
        NetrunnerHackKind.NeuralOverload => "fish-netrunner-program-neural-overload",
        NetrunnerHackKind.ShortCircuit => "fish-netrunner-program-short-circuit",
        NetrunnerHackKind.MotorInterference => "fish-netrunner-program-motor-interference",
        NetrunnerHackKind.OpticalGlitch => "fish-netrunner-program-optical-glitch",
        NetrunnerHackKind.LockWeapon => "fish-netrunner-program-lock-weapon",
        NetrunnerHackKind.GripShock => "fish-netrunner-program-grip-shock",
        NetrunnerHackKind.NeuralStatic => "fish-netrunner-program-neural-static",
        NetrunnerHackKind.ThermalSurge => "fish-netrunner-program-thermal-surge",
        NetrunnerHackKind.BatteryDrain => "fish-netrunner-program-battery-drain",
        NetrunnerHackKind.LockAirlock => "fish-netrunner-program-lock-airlock",
        NetrunnerHackKind.EraseTrace => "fish-netrunner-program-erase-trace",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}

/// <summary>A targeted action referencing a program definition.</summary>
public sealed partial class NetrunnerHackActionEvent : EntityTargetActionEvent
{
    [DataField(required: true)] public ProtoId<NetrunnerAbilityPrototype> Ability;
}

/// <summary>A program aimed at a visible location rather than one device.</summary>
public sealed partial class NetrunnerAreaHackActionEvent : WorldTargetActionEvent
{
    [DataField(required: true)] public ProtoId<NetrunnerAbilityPrototype> Ability;
}

/// <summary>Opens the implant terminal.</summary>
public sealed partial class NetrunnerTerminalActionEvent : InstantActionEvent;

/// <summary>Returns the mobile netrunner eye back to the chair.</summary>
public sealed partial class NetrunnerJumpToChairActionEvent : InstantActionEvent;

/// <summary>Completes the one currently reserved program.</summary>
[Serializable, NetSerializable]
public sealed partial class NetrunnerHackDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Completes connection to a powered chair.</summary>
[Serializable, NetSerializable]
public sealed partial class NetrunnerConnectDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Checks a program's device-specific conditions without changing the device.</summary>
[ByRefEvent]
public record struct NetrunnerValidateHackEvent(EntityUid User, NetrunnerAbilityPrototype Ability)
{
    public bool Supported;
    public bool Allowed;
    public EntityUid? Device;
}

/// <summary>Applies an already validated program and reports whether it took effect.</summary>
[ByRefEvent]
public record struct NetrunnerApplyHackEvent(EntityUid User, EntityUid Device, NetrunnerAbilityPrototype Ability)
{
    public bool Applied;
}
