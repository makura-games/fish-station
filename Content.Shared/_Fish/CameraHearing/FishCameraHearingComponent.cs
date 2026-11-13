namespace Content.Shared._Fish.CameraHearing;

/// <summary>
/// Сущность слышит речь рядом с собой и передаёт её ИИ и мониторам камер.
/// </summary>
[RegisterComponent]
public sealed partial class FishCameraHearingComponent : Component
{
    [DataField]
    public float Range = 4f;

    [DataField]
    public float WhisperRange = 2f;
}
