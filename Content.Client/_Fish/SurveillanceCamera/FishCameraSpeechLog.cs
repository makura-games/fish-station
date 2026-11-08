using System.Numerics;
using Content.Client.Chat.UI;
using Content.Client.Viewport;
using Content.Shared.Chat;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.SurveillanceCamera.UI;

public sealed partial class FishCameraSpeechLog : Control
{
    [Dependency] private IEntityManager _entMan = default!;

    private const float HeadOffset = 0.5f;

    private readonly Dictionary<EntityUid, SpeechBubble> _bubbles = new();
    private ScalingViewport? _viewport;

    public FishCameraSpeechLog()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    public void AddLine(ScalingViewport viewport, NetEntity speaker, string name, string message, bool whisper)
    {
        if (!_entMan.TryGetEntity(speaker, out var uid))
            return;

        _viewport = viewport;

        if (_bubbles.Remove(uid.Value, out var old))
            RemoveChild(old);

        var wrapped = $"[BubbleHeader]{FormattedMessage.EscapeText(name)}[/BubbleHeader][BubbleContent]{FormattedMessage.EscapeText(message)}[/BubbleContent]";
        var chatMessage = new ChatMessage(whisper ? ChatChannel.Whisper : ChatChannel.Local, message, wrapped, speaker, null);
        var type = whisper ? SpeechBubble.SpeechType.Whisper : SpeechBubble.SpeechType.Say;
        var bubble = SpeechBubble.CreateSpeechBubble(type, chatMessage, uid.Value);

        bubble.MouseFilter = MouseFilterMode.Ignore;
        bubble.OnDied += OnBubbleDied;
        _bubbles[uid.Value] = bubble;
        AddChild(bubble);
    }

    private void OnBubbleDied(EntityUid speaker, SpeechBubble bubble)
    {
        if (_bubbles.TryGetValue(speaker, out var current) && current == bubble)
            _bubbles.Remove(speaker);

        RemoveChild(bubble);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        InvalidateArrange();
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (_viewport?.Eye is not { } eye)
            return finalSize;

        var transform = _entMan.System<SharedTransformSystem>();

        foreach (var (speaker, bubble) in _bubbles)
        {
            if (!_entMan.EntityExists(speaker))
                continue;

            var offset = (-eye.Rotation).ToWorldVec() * -HeadOffset;
            var screen = _viewport.WorldToScreen(transform.GetWorldPosition(speaker) + offset);
            var local = (screen - GlobalPixelPosition) / UIScale;

            bubble.Measure(Vector2Helpers.Infinity);
            bubble.Arrange(UIBox2.FromDimensions(local - new Vector2(bubble.ContentSize.X / 2f, bubble.ContentSize.Y), bubble.DesiredSize));
        }

        return finalSize;
    }
}
