using Content.Shared._Fish.Cyberpsychosis;
using Content.Shared.Chat;
using Robust.Client.Replays.Playback;
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
    [Dependency] private IReplayPlaybackManager _cyberpsychosisReplay = default!;

    private ChatMessage ApplyCyberpsychosisSpeechDistortion(ChatMessage msg)
    {
        // Never mutate stored replay messages during replay playback or scrubbing.
        if (_cyberpsychosisReplay.Replay != null)
            return msg;

        if (msg.Channel is not (ChatChannel.Local or ChatChannel.Whisper or ChatChannel.Radio))
            return msg;

        if (string.IsNullOrEmpty(msg.Message))
            return msg;

        if (_player.LocalEntity is not { } local
            || !_ent.TryGetComponent(local, out CyberpsychosisComponent? psycho)
            || psycho.Level < CyberpsychosisSpeechThreshold)
            return msg;

        // Do not distort the player's own local/whispered voice to themselves.
        if (msg.Channel != ChatChannel.Radio && msg.SenderEntity == _ent.GetNetEntity(local))
            return msg;

        var original = msg.Message;
        var distorted = DistortCyberpsychosisSpeech(original);
        var escapedOriginal = FormattedMessage.EscapeText(original);
        var escapedDistorted = FormattedMessage.EscapeText(distorted);

        var copy = new ChatMessage(msg);
        if (TryReplaceLastOutsideTags(copy.WrappedMessage, escapedOriginal, escapedDistorted, out var updated)
            || TryReplaceLastOutsideTags(copy.WrappedMessage, original, escapedDistorted, out updated))
        {
            copy.WrappedMessage = updated;
        }

        copy.Message = distorted;
        return copy;
    }

    private static bool TryReplaceLastOutsideTags(string wrapped, string needle, string replacement, out string result)
    {
        result = wrapped;
        if (string.IsNullOrEmpty(needle) || string.IsNullOrEmpty(wrapped))
            return false;

        var matchIndex = -1;
        var inTag = false;
        var textStart = 0;

        for (var i = 0; i < wrapped.Length; i++)
        {
            var c = wrapped[i];
            if (!inTag)
            {
                if (c == '\\' && i + 1 < wrapped.Length)
                {
                    i++;
                    continue;
                }

                if (c == '[')
                {
                    var found = FindLastInSpan(wrapped, textStart, i, needle);
                    if (found >= 0)
                        matchIndex = found;
                    inTag = true;
                }
            }
            else if (c == ']')
            {
                inTag = false;
                textStart = i + 1;
            }
        }

        if (!inTag && textStart < wrapped.Length)
        {
            var found = FindLastInSpan(wrapped, textStart, wrapped.Length, needle);
            if (found >= 0)
                matchIndex = found;
        }

        if (matchIndex < 0)
            return false;

        result = string.Concat(
            wrapped.AsSpan(0, matchIndex),
            replacement,
            wrapped.AsSpan(matchIndex + needle.Length));
        return true;
    }

    private static int FindLastInSpan(string text, int start, int end, string needle)
    {
        var length = end - start;
        if (length < needle.Length)
            return -1;

        var localIndex = text.AsSpan(start, length).LastIndexOf(needle.AsSpan(), StringComparison.Ordinal);
        return localIndex >= 0 ? start + localIndex : -1;
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
