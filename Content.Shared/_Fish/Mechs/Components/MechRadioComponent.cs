using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Встроенное радио меха: mic/speaker через IntrinsicRadio + ActiveRadio.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechRadioComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool MicrophoneEnabled = true;

    [DataField, AutoNetworkedField]
    public bool SpeakerEnabled = true;

    [DataField]
    public EntProtoId ToggleMicAction = "ActionMechToggleRadioMic";

    [DataField]
    public EntProtoId ToggleSpeakerAction = "ActionMechToggleRadioSpeaker";

    [DataField]
    public EntityUid? ToggleMicActionEntity;

    [DataField]
    public EntityUid? ToggleSpeakerActionEntity;
}
