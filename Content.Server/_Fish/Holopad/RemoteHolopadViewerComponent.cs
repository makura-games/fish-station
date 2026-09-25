namespace Content.Server.Holopad;

/// <summary>
/// Не выдавать вручную!
/// </summary>
[RegisterComponent]
public sealed partial class RemoteHolopadViewerComponent : Component
{
    public EntityUid Transmitter;
    public EntityUid? ExitViewActionEntity;
}
