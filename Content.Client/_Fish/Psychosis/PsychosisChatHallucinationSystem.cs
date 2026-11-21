using System;
using Content.Shared._Fish.Psychosis;
using Content.Shared.Chat;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Client.UserInterface.Systems.Chat;

namespace Content.Client._Fish.Psychosis;

/// <summary>
/// Иногда показывает игроку в чате шёпот, которого не было.
/// </summary>
public sealed partial class PsychosisChatHallucinationSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const float MinDelay = 45f;
    private const float MaxDelay = 120f;
    private const float MinDistance = 2f;
    private const float MaxDistance = 9f;

    private static readonly string[] WhisperKeys =
    {
        "fish-psychosis-whisper-1",
        "fish-psychosis-whisper-2",
        "fish-psychosis-whisper-3",
        "fish-psychosis-whisper-4",
    };

    private ChatUIController? _chat;
    private float _currentPsychosisLevel;
    private float _nextWhisperTime;
    private readonly HashSet<EntityUid> _nearby = new();

    public override void Initialize()
    {
        base.Initialize();

        _chat = _ui.GetUIController<ChatUIController>();
        SubscribeLocalEvent<PsychosisUpdateEvent>(OnPsychosisUpdate);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalPlayerDetached);
    }

    private void OnPsychosisUpdate(PsychosisUpdateEvent ev)
    {
        _currentPsychosisLevel = ev.Level;
    }

    private void OnLocalPlayerDetached(LocalPlayerDetachedEvent ev)
    {
        _currentPsychosisLevel = 0f;
        _nextWhisperTime = 0f;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted || _currentPsychosisLevel < PsychosisStages.DeliriousMin)
            return;

        var player = _player.LocalSession?.AttachedEntity;
        if (player == null)
            return;

        _nextWhisperTime -= frameTime;
        if (_nextWhisperTime > 0f)
            return;

        _nextWhisperTime = _random.NextFloat(MinDelay, MaxDelay);
        TrySendFakeWhisper(player.Value);
    }

    /// <summary>
    /// Отправляет клиенту ложное сообщение от имени находящегося рядом игрока.
    /// </summary>
    private void TrySendFakeWhisper(EntityUid player)
    {
        if (_chat == null)
            return;

        var playerPosition = _transform.GetMapCoordinates(player);
        _nearby.Clear();
        _lookup.GetEntitiesInRange(_transform.ToCoordinates(playerPosition), MaxDistance, _nearby, LookupFlags.Dynamic);

        EntityUid? sender = null;
        var closest = float.MaxValue;
        foreach (var candidate in _nearby)
        {
            if (candidate == player ||
                _player.LocalSession?.AttachedEntity == candidate ||
                !HasComp<ActorComponent>(candidate))
                continue;

            var distance = (_transform.GetMapCoordinates(candidate).Position - playerPosition.Position).Length();
            if (distance < MinDistance || distance >= closest)
                continue;

            closest = distance;
            sender = candidate;
        }

        if (sender == null)
            return;

        var name = FormattedMessage.EscapeText(MetaData(sender.Value).EntityName);
        var text = FormattedMessage.EscapeText(
            Loc.GetString(_random.Pick(WhisperKeys), ("name", MetaData(sender.Value).EntityName)));

        var message = new ChatMessage(
            ChatChannel.Whisper,
            text,
            $"[color=#b06fa8]{name}[/color] шёпотом: {text}",
            GetNetEntity(sender.Value),
            null);

        _chat.ProcessChatMessage(message);
    }
}
