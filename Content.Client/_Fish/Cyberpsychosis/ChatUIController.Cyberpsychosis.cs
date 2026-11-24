using Content.Shared._Fish.Cyberpsychosis;
using Content.Shared.Chat;
using Robust.Shared.Random;
using Robust.Shared.Utility;

#pragma warning disable IDE0130 // Fish-edit: Расширяем контроллер чата для искажения слышимой речи при киберпсихозе 90%+.
namespace Content.Client.UserInterface.Systems.Chat;

public sealed partial class ChatUIController
{
    private const float CyberpsychosisSpeechThreshold = 90f;
    private const int CyberpsychosisShiftMin = 5;
    private const int CyberpsychosisShiftMax = 200;

    [Dependency] private IRobustRandom _cyberpsychosisRandom = default!;

    private void ApplyCyberpsychosisSpeechDistortion(ChatMessage msg)
    {
        if (msg.Channel is not (ChatChannel.Local or ChatChannel.Whisper or ChatChannel.Radio))
            return;

        if (string.IsNullOrEmpty(msg.Message))
            return;

        if (_player.LocalEntity is not { } local
            || !_ent.TryGetComponent(local, out CyberpsychosisComponent? psycho)
            || psycho.Level < CyberpsychosisSpeechThreshold)
            return;

        // Do not distort the player's own local/whispered voice to themselves.
        if (msg.Channel != ChatChannel.Radio && msg.SenderEntity == _ent.GetNetEntity(local))
            return;

        var original = msg.Message;
        var distorted = DistortCyberpsychosisSpeech(original);
        var escapedOriginal = FormattedMessage.EscapeText(original);
        var escapedDistorted = FormattedMessage.EscapeText(distorted);

        var index = msg.WrappedMessage.LastIndexOf(escapedOriginal, StringComparison.Ordinal);
        if (index >= 0)
        {
            msg.WrappedMessage = string.Concat(
                msg.WrappedMessage.AsSpan(0, index),
                escapedDistorted,
                msg.WrappedMessage.AsSpan(index + escapedOriginal.Length));
        }
        else
        {
            index = msg.WrappedMessage.LastIndexOf(original, StringComparison.Ordinal);
            if (index >= 0)
            {
                msg.WrappedMessage = string.Concat(
                    msg.WrappedMessage.AsSpan(0, index),
                    escapedDistorted,
                    msg.WrappedMessage.AsSpan(index + original.Length));
            }
        }

        msg.Message = distorted;
    }

    private string DistortCyberpsychosisSpeech(string message)
    {
        var chars = message.ToCharArray();

        for (var i = 0; i < chars.Length; i++)
        {
            if (!_cyberpsychosisRandom.Prob(0.5f))
                continue;

            // Matches AnomalyAccentSystem Unicode character shift.
            var shifted = chars[i] + _cyberpsychosisRandom.Next(CyberpsychosisShiftMin, CyberpsychosisShiftMax);
            if (shifted < 0xD800)
                chars[i] = (char) shifted;
        }

        return new string(chars);
    }
}
