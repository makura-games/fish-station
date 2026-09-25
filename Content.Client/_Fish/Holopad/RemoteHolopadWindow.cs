using Content.Shared.Holopad;
using Content.Shared.Telephone;

namespace Content.Client.Holopad;


public sealed partial class HolopadWindow
{


    partial void UpdateRemoteAppearance(bool lockButtons)
    {
        if (_owner is not { } owner)
            return;

        var isTransmitter = _entManager.HasComponent<RemoteHolopadTransmitterComponent>(owner);

        var isReceiver = _entManager.HasComponent<RemoteHolopadReceiverComponent>(owner);


        if (isReceiver)
        {
            EndCallButton.Disabled = true;
            EndCallButton.Visible = false;
        }


        if (isTransmitter || isReceiver)
        {
            RequestStationAiButton.Disabled = true;
            RequestStationAiButton.Visible = false;
            StartBroadcastButton.Disabled = true;
            StartBroadcastButton.Visible = false;
        }
    }
}
