using System.Collections.Generic;
using Content.Server.Antag;
using Content.Server.Mind;
using Content.Shared._Fish.Antag;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._Fish.Antag;

public sealed partial class FishAntagRulesServerSystem : EntitySystem
{
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private ISharedPlayerManager _player = default!;

    private readonly Dictionary<EntityUid, (ICommonSession Session, HashSet<string> Rules)> _shownRules = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AfterAntagEntitySelectedEvent>(OnAntagSelected);
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<RoleRemovedEvent>(OnRoleRemoved);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    /// <summary>
    /// Показывает правила антагониста после его выбора для игрока.
    /// </summary>
    private void OnAntagSelected(ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Session is not { } session || !_mind.TryGetMind(session, out var mindId, out var mind))
            return;

        ShowNewRules(mindId, mind, session);
    }

    /// <summary>
    /// Показывает правила, когда игрок получает новую роль.
    /// </summary>
    private void OnRoleAdded(RoleAddedEvent args)
    {
        if (args.Mind.UserId is not { } userId || !_player.TryGetSessionById(userId, out var session))
            return;

        ShowNewRules(args.MindId, args.Mind, session);
    }

    /// <summary>
    /// Повторно показывает правила после подключения игрока к разуму.
    /// </summary>
    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (!_mind.TryGetMind(args.Player, out var mindId, out var mind))
            return;

        ShowNewRules(mindId, mind, args.Player);
    }

    /// <summary>
    /// Удаляет учёт правил для ролей, которых больше нет у разума.
    /// </summary>
    private void OnRoleRemoved(RoleRemovedEvent args)
    {
        if (!_shownRules.TryGetValue(args.MindId, out var shownRules))
            return;

        shownRules.Rules.IntersectWith(GetMindRules(args.Mind));
        if (shownRules.Rules.Count == 0)
            _shownRules.Remove(args.MindId);
    }

    /// <summary>
    /// Очищает учёт правил при перезапуске раунда.
    /// </summary>
    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _shownRules.Clear();
    }

    /// <summary>
    /// Отправляет текущей сессии ещё не показанные правила ролей разума.
    /// </summary>
    private void ShowNewRules(EntityUid mindId, MindComponent mind, ICommonSession session)
    {
        if (session.Status is SessionStatus.Disconnected or SessionStatus.Zombie)
            return;

        var rules = GetMindRules(mind);
        if (rules.Count == 0)
            return;

        if (!_shownRules.TryGetValue(mindId, out var shownRules) || !ReferenceEquals(shownRules.Session, session))
        {
            shownRules = (session, new HashSet<string>());
            _shownRules[mindId] = shownRules;
        }

        var hasNewRules = false;
        foreach (var rule in rules)
        {
            if (shownRules.Rules.Add(rule))
                hasNewRules = true;
        }

        if (hasNewRules)
            RaiseNetworkEvent(new FishAntagRulesEvent([.. rules]), session);
    }

    /// <summary>
    /// Собирает уникальные правила антагонистических ролей разума.
    /// </summary>
    private List<string> GetMindRules(MindComponent mind)
    {
        var rules = new List<string>();
        foreach (var role in mind.MindRoleContainer.ContainedEntities)
        {
            if (!TryComp<MindRoleComponent>(role, out var mindRole) || mindRole.AntagPrototype is not { } antagId)
                continue;

            var locId = ProtoMan.Index(antagId).Rules?.Id;
            if (!string.IsNullOrWhiteSpace(locId) && !rules.Contains(locId))
                rules.Add(locId);
        }

        return rules;
    }
}
