using Robust.Shared.Serialization;

namespace Content.Shared._Fish.ClockworkCult;

[Serializable, NetSerializable]
public enum FishClockworkScriptureTier : byte
{
    Driver = 0,
    Script = 1,
    Application = 2,
}

[Serializable, NetSerializable]
public enum FishClockworkSigilType : byte
{
    Transgression = 0,
    Submission = 1,
    Vitality = 2,
    Transmission = 3,
}

[Serializable, NetSerializable]
public enum FishClockworkArkPhase : byte
{
    Prep = 0,
    Activating = 1,
    Grace = 2,
    Defense = 3,
    RatvarArrival = 4,
    Destroyed = 5,
}

[Serializable, NetSerializable]
public enum FishClockworkWinCondition : byte
{
    Ongoing = 0,
    CultWin = 1,
    CultFailure = 2,
}
