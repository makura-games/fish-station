using Robust.Shared.Serialization;

namespace Content.Shared._Fish.CameraHearing;

[Serializable, NetSerializable]
public sealed class FishCameraSpeechMessage(NetEntity speaker, string name, string message, bool whisper) : BoundUserInterfaceMessage
{
    public NetEntity Speaker { get; } = speaker;
    public string Name { get; } = name;
    public string Message { get; } = message;
    public bool Whisper { get; } = whisper;
}
