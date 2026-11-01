using Robust.Shared.Serialization;
using Robust.Shared.UserInterface;

namespace Content.Shared._Fish.Xenobiology.Machines;

[Serializable, NetSerializable]
public enum FishXenobiologyConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FishXenobiologyConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly int StoredCount;
    public readonly int MaxStored;
    public readonly int MonkeyCubes;
    public readonly string? LoadedPotionName;
    public readonly string? StatusMessage;

    public FishXenobiologyConsoleBoundUserInterfaceState(
        int storedCount,
        int maxStored,
        int monkeyCubes,
        string? loadedPotionName,
        string? statusMessage)
    {
        StoredCount = storedCount;
        MaxStored = maxStored;
        MonkeyCubes = monkeyCubes;
        LoadedPotionName = loadedPotionName;
        StatusMessage = statusMessage;
    }
}

[Serializable, NetSerializable]
public sealed class FishXenoConsoleReleaseAllMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FishXenoConsoleCollectSlimesMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FishXenoConsoleEjectMonkeyMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FishXenoConsoleScanStoredMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FishXenoConsoleFeedPotionMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FishXenoConsoleRecycleMonkeysMessage : BoundUserInterfaceMessage;
