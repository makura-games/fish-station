using Robust.Shared.Serialization;

namespace Content.Shared._Fish.ClockworkCult.Components;

[Serializable, NetSerializable]
public enum FishClockworkTrapKind : byte
{
    Generic = 0,
    BrassSkewer = 1,
    SteamVent = 2,
    PressureSensor = 3,
    Lever = 4,
    Repeater = 5,
}
