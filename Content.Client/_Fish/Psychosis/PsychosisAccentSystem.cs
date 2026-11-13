using System.Text;
using Content.Shared._Fish.Psychosis;
using Content.Shared.Chat;
using Content.Shared.Popups;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Client.UserInterface.Systems.Chat;

namespace Content.Client._Fish.Psychosis;

/// <summary>
/// Искажает входящие сообщения чата в зависимости от уровня психоза игрока.
/// Три уровня: лёгкий, средний, тяжёлый.
/// </summary>
public sealed partial class PsychosisAccentSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private float _currentPsychosisLevel;
    private ChatUIController _chatController = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PsychosisUpdateEvent>(OnPsychosisUpdate);
        _chatController = _ui.GetUIController<ChatUIController>();
        _chatController.MessagePreparing += OnChatMessagePreparing;
    }

    public override void Shutdown()
    {
        _chatController.MessagePreparing -= OnChatMessagePreparing;
        base.Shutdown();
    }

    /// <summary>
    /// Обновляет психоз локального игрока и показывает сообщение при его начале.
    /// </summary>
    private void OnPsychosisUpdate(PsychosisUpdateEvent ev)
    {
        if (!ev.IsInitialState && _currentPsychosisLevel <= 0f && ev.Level > 0f &&
            _player.LocalSession?.AttachedEntity is { } entity)
        {
            _popup.PopupEntity(Loc.GetString("fish-psychosis-onset"), entity, entity);
        }

        _currentPsychosisLevel = ev.Level;
    }

    /// <summary>
    /// Искажает локальные и радиосообщения в соответствии с текущим уровнем психоза.
    /// </summary>
    private void OnChatMessagePreparing(ChatMessage message)
    {
        if (_currentPsychosisLevel <= 0)
            return;

        if ((message.Channel & ChatChannel.IC) == 0)
            return;

        if (string.IsNullOrEmpty(message.Message))
            return;

        var wrapped = FormattedMessage.FromMarkupPermissive(message.WrappedMessage);
        var wrappedText = wrapped.ToString();
        var body = message.Message;
        var bodyStart = wrappedText.LastIndexOf(body, StringComparison.Ordinal);
        if (bodyStart < 0)
        {
            body = FormattedMessage.RemoveMarkupPermissive(message.Message);
            bodyStart = wrappedText.LastIndexOf(body, StringComparison.Ordinal);
        }

        if (bodyStart < 0)
            return;

        var bodyEnd = bodyStart + body.Length;
        var wrappedBuilder = new StringBuilder(message.WrappedMessage.Length);
        var bodyBuilder = new StringBuilder(body.Length);
        var textOffset = 0;

        foreach (var node in wrapped)
        {
            if (!node.IsPlainText)
            {
                wrappedBuilder.Append(node);
                continue;
            }

            var text = node.Value.StringValue ?? string.Empty;
            var textEnd = textOffset + text.Length;
            var overlapStart = Math.Max(textOffset, bodyStart);
            var overlapEnd = Math.Min(textEnd, bodyEnd);
            if (overlapStart < overlapEnd)
            {
                var prefixLength = overlapStart - textOffset;
                var bodyLength = overlapEnd - overlapStart;
                var prefix = text[..prefixLength];
                var bodyText = text.Substring(prefixLength, bodyLength);
                var suffix = text[(overlapEnd - textOffset)..];
                var distorted = Distort(bodyText);

                wrappedBuilder.Append(FormattedMessage.EscapeText(prefix));
                wrappedBuilder.Append(FormattedMessage.EscapeText(distorted));
                wrappedBuilder.Append(FormattedMessage.EscapeText(suffix));
                bodyBuilder.Append(distorted);
            }
            else
            {
                wrappedBuilder.Append(FormattedMessage.EscapeText(text));
            }

            textOffset = textEnd;
        }

        message.WrappedMessage = wrappedBuilder.ToString();
        message.Message = bodyBuilder.ToString();
    }

    private string Distort(string text)
    {
        if (_currentPsychosisLevel < 33)
            return DistortLight(text);
        if (_currentPsychosisLevel < 66)
            return DistortMedium(text);
        return DistortHeavy(text);
    }

    /// <summary>
    /// Лёгкое искажение: случайная замена некоторых букв на символы.
    /// </summary>
    private string DistortLight(string text)
    {
        var chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (_random.Prob(0.1f)) // 10% шанс
            {
                chars[i] = _random.Pick(new[] { '?', '!', '.', ',', '#', '*' });
            }
        }
        return new string(chars);
    }

    /// <summary>
    /// Среднее искажение: перестановка букв в словах, добавление мусорных слов.
    /// </summary>
    private string DistortMedium(string text)
    {
        var words = text.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 3 && _random.Prob(0.3f))
            {
                words[i] = ShuffleLetters(words[i]);
            }
            else if (_random.Prob(0.1f))
            {
                words[i] = _random.Pick(new[] { "кхм", "да" });
            }
        }
        return string.Join(' ', words);
    }

    /// <summary>
    /// Тяжёлое искажение: полная замена букв случайными, текст превращается в нечитаемый поток.
    /// </summary>
    private string DistortHeavy(string text)
    {
        return PsychosisTextDistortion.DistortToNonsense(text, _random);
    }

    /// <summary>
    /// Перемешивает буквы в слове (кроме первой и последней).
    /// </summary>
    private string ShuffleLetters(string word)
    {
        if (word.Length < 4)
            return word;

        var chars = word.ToCharArray();
        int start = 1;
        int end = chars.Length - 2;
        for (int i = start; i <= end; i++)
        {
            int j = _random.Next(start, end + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }
}