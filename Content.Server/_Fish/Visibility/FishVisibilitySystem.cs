using Content.Shared.CCVar;
using Content.Server.Administration.Managers;
using Content.Shared.Eye;
using Content.Shared.Mind.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

using Content.Shared.GameTicking;
namespace Content.Server._Fish.Visibility;

/// <summary>
/// Решает, кому какое состояние вообще уходит по сети.
///
/// Обычный игрок получает слой Normal. Призраки и наблюдатели — Ghost, администраторы — Admin.
/// Сущность, помеченная слоем Admin|Ghost, не сериализуется в состояние обычного клиента:
/// чит не сможет её ни отрисовать, ни прочитать из буфера, потому что этих данных там нет.
/// </summary>
public sealed partial class FishVisibilitySystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedVisibilitySystem _visibility = default!;

    /// <summary>Слой, который видят только администраторы и наблюдатели.</summary>
    public const ushort AdminOnlyLayer =
        (ushort)((int)VisibilityFlags.Admin | (int)VisibilityFlags.Ghost);

    private readonly HashSet<string> _hidden = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Сущностями, которые уже разобрали, не занимаемся повторно.</summary>
    private readonly HashSet<EntityUid> _seen = new();

    private bool _enabled;

    private TimeSpan _nextMaskSweep;

    private EntityQuery<VisibilityComponent> _visQuery = default!;
    private EntityQuery<ActorComponent> _actorQuery = default!;
    private EntityQuery<MindContainerComponent> _mindQuery = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("fish.visibility");

        _visQuery = GetEntityQuery<VisibilityComponent>();
        _actorQuery = GetEntityQuery<ActorComponent>();
        _mindQuery = GetEntityQuery<MindContainerComponent>();

        Subs.CVar(_cfg, FishCVars.VisibilityLayerEnabled, value => _enabled = value, true);
        Subs.CVar(_cfg, FishCVars.VisibilityAdminOnlyPrototypes, ParseList, true);

        SubscribeLocalEvent<GetVisMaskEvent>(OnGetMask);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);

        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    /// <summary>Сколько прототипов сейчас скрыто.</summary>
    public int HiddenCount => _hidden.Count;

    private void ParseList(string text)
    {
        _hidden.Clear();

        if (string.IsNullOrWhiteSpace(text))
            return;

        var parts = text.Split(new[] { ',', ';', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var id = part.Trim();
            if (id.Length > 0)
                _hidden.Add(id);
        }

        // Список поменяли на живой сервере — пересматриваем мир заново.
        _seen.Clear();
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        _seen.Clear();
        RefreshAdminMasks();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.InGame)
            return;

        if (args.Session.AttachedEntity is { } entity)
            _eye.RefreshVisibilityMask(entity);
    }

    /// <summary>
    /// Администратор в своём теле должен видеть то, что скрыто от экипажа. Призракам слой
    /// Ghost добавляет их собственная система, её и не трогаем.
    /// </summary>
    private void OnGetMask(ref GetVisMaskEvent ev)
    {
        if (!_enabled)
            return;

        if (!_actorQuery.TryComp(ev.Entity, out var actor))
            return;

        if (_admins.GetAdminData(actor.PlayerSession) is not { Active: true })
            return;

        ev.VisibilityMask |= (int)VisibilityFlags.Admin;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled)
            return;

        if (_hidden.Count > 0)
        {
            // Появляются и после старта раунда: спавн событий, разморозка, сборка карты.
            var query = AllEntityQuery<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var meta))
            {
                if (!_seen.Add(uid))
                    continue;

                if (meta.EntityLifeStage < EntityLifeStage.MapInitialized)
                    continue;

                var prototype = meta.EntityPrototype;
                if (prototype == null || !_hidden.Contains(prototype.ID))
                    continue;

                Hide(uid, prototype.ID);
            }
        }

        // Права администратора могут выдать и снять посреди раунда.
        if (_timing.CurTime >= _nextMaskSweep)
        {
            _nextMaskSweep = _timing.CurTime + TimeSpan.FromSeconds(20);
            RefreshAdminMasks();
        }
    }

    /// <summary>
    /// Скрыть сущность от экипажа: её состояние перестаёт уходить обычным клиентам.
    /// </summary>
    /// <returns>false — прятать отказались.</returns>
    public bool Hide(EntityUid uid, string? prototype = null)
    {
        if (!_enabled)
            return false;

        // Игрока и тело с привязанным разумом скрывать нельзя: это ломает игру, а не чит.
        // Список прототипов правят на живом сервере, опечатка в нём стоит дорого.
        if (_actorQuery.HasComp(uid) ||
            _mindQuery.TryComp(uid, out var mind) && mind.HasMind)
        {
            _sawmill.Warning($"Refused to hide {prototype ?? ToPrettyString(uid)}: belongs to a player.");
            return false;
        }

        _visibility.SetLayer((uid, _visQuery.CompOrNull(uid)), AdminOnlyLayer);
        return true;
    }

    /// <summary>Вернуть сущность обычным клиентам.</summary>
    public void Reveal(EntityUid uid)
    {
        _visibility.SetLayer((uid, _visQuery.CompOrNull(uid)), (ushort)VisibilityFlags.Normal);
    }

    private void RefreshAdminMasks()
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } entity)
                continue;

            _eye.RefreshVisibilityMask(entity);
        }
    }
}
