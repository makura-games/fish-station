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

        SubscribeNetworkEvent<PsychosisUpdateEvent>(OnPsychosisUpdate);
        _chatController = _ui.GetUIController<ChatUIController>();
        _chatController.MessageAdded += OnChatMessageAdded;
    }

    public override void Shutdown()
    {
        _chatController.MessageAdded -= OnChatMessageAdded;
        base.Shutdown();
    }

    private void OnPsychosisUpdate(PsychosisUpdateEvent ev)
    {
        if (GetEntity(ev.Uid) != _player.LocalSession?.AttachedEntity)
            return;

        if (_currentPsychosisLevel <= 0f && ev.Level > 0f &&
            _player.LocalSession?.AttachedEntity is { } entity)
        {
            _popup.PopupClient(Loc.GetString("fish-psychosis-onset"), entity, entity);
        }

        _currentPsychosisLevel = ev.Level;
    }

    private void OnChatMessageAdded(ChatMessage message)
    {
        if (_currentPsychosisLevel <= 0)
            return;

        if (message.Channel != ChatChannel.Local && message.Channel != ChatChannel.Radio)
            return;

        string newText;
        if (_currentPsychosisLevel < 33)
            newText = DistortLight(message.Message);
        else if (_currentPsychosisLevel < 66)
            newText = DistortMedium(message.Message);
        else
            newText = DistortHeavy(message.Message);

        var oldText = FormattedMessage.EscapeText(message.Message);
        var escapedText = FormattedMessage.EscapeText(newText);
        message.WrappedMessage = message.WrappedMessage.Replace(oldText, escapedText, StringComparison.Ordinal);
        message.Message = newText;
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
        var chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsLetter(chars[i]))
            {
                // Случайная буква русского или английского алфавита
                chars[i] = _random.Prob(0.5f)
                    ? (char)_random.Next('а', 'я' + 1)
                    : (char)_random.Next('a', 'z' + 1);
            }
            else if (char.IsDigit(chars[i]))
            {
                chars[i] = (char)_random.Next('0', '9' + 1);
            }
            // Знаки препинания остаются для структуры
        }
        return new string(chars);
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