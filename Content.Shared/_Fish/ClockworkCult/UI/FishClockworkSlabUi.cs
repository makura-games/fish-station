using Robust.Shared.Serialization;

namespace Content.Shared._Fish.ClockworkCult.UI;

[Serializable, NetSerializable]
public enum FishClockworkSlabUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FishClockworkSlabBoundUserInterfaceState : BoundUserInterfaceState
{
    public float Power;
    public float Vitality;
    public int ServantCount;
    public bool ScriptUnlocked;
    public bool ApplicationUnlocked;
    public bool Heralded;
    public TimeSpan? ArkTimeRemaining;
    public FishClockworkArkPhase ArkPhase;
    public List<FishClockworkScriptureEntry> Scriptures;

    public FishClockworkSlabBoundUserInterfaceState(
        float power,
        float vitality,
        int servantCount,
        bool scriptUnlocked,
        bool applicationUnlocked,
        bool heralded,
        TimeSpan? arkTimeRemaining,
        FishClockworkArkPhase arkPhase,
        List<FishClockworkScriptureEntry> scriptures)
    {
        Power = power;
        Vitality = vitality;
        ServantCount = servantCount;
        ScriptUnlocked = scriptUnlocked;
        ApplicationUnlocked = applicationUnlocked;
        Heralded = heralded;
        ArkTimeRemaining = arkTimeRemaining;
        ArkPhase = arkPhase;
        Scriptures = scriptures;
    }
}

[Serializable, NetSerializable]
public sealed class FishClockworkScriptureEntry
{
    public string PrototypeId;
    public string Name;
    public string Description;
    public FishClockworkScriptureTier Tier;
    public float PowerCost;
    public float ChannelTime;
    public int InvokersRequired;
    public bool Available;
    public string? UnavailableReason;

    public FishClockworkScriptureEntry(
        string prototypeId,
        string name,
        string description,
        FishClockworkScriptureTier tier,
        float powerCost,
        float channelTime,
        int invokersRequired,
        bool available,
        string? unavailableReason)
    {
        PrototypeId = prototypeId;
        Name = name;
        Description = description;
        Tier = tier;
        PowerCost = powerCost;
        ChannelTime = channelTime;
        InvokersRequired = invokersRequired;
        Available = available;
        UnavailableReason = unavailableReason;
    }
}

[Serializable, NetSerializable]
public sealed class FishClockworkSlabReciteMessage : BoundUserInterfaceMessage
{
    public string ScripturePrototypeId;

    public FishClockworkSlabReciteMessage(string scripturePrototypeId)
    {
        ScripturePrototypeId = scripturePrototypeId;
    }
}
