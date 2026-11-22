using System.Linq;
using Content.Shared.CCVar;
using Content.Server.Administration.Managers;
using Content.Shared.Eye;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Visibility;

/// <summary>
/// Решает, кому какое состояние вообще уходит по сети.
///
/// Обычный игрок получает слой Normal. Сущность, снятая с Normal, не сериализуется в
/// состояние обычного клиента: чит не сможет её ни отрисовать, ни прочитать из буфера,
/// потому что этих данных там просто нет.
///
/// Слой — один бит, а не сочетание Admin | Ghost. PVS отдаёт сущность зрителю только тогда,
/// когда маска зрителя содержит все биты её слоя, поэтому «Admin | Ghost» требовало бы от
/// зрителя обоих битов и не показал бы сущность ни администратору, ни наблюдателю. Вместо
/// этого введён собственный бит FishAdminOnly, который эта система выдаёт и администраторам,
/// и наблюдателям.
/// </summary>
public sealed partial class FishVisibilitySystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedVisibilitySystem _visibility = default!;

    /// <summary>Слой, который видят администраторы и наблюдатели.</summary>
    public const ushort AdminOnlyLayer = (ushort)VisibilityFlags.FishAdminOnly;

    /// <summary>Прототипы, которые прячем от обычных клиентов.</summary>
    private readonly HashSet<string> _hidden = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Спрятанные сущности: прототип и слой, который был до нас. Нужно, чтобы снять
    /// ограничение, когда прототип убрали из списка или систему выключили.
    /// </summary>
    private readonly Dictionary<EntityUid, (string Prototype, ushort PreviousLayer)> _managed = new();

    /// <summary>Сущности, чью видимость мы уже разобрали: повторно не сканируем.</summary>
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

        Subs.CVar(_cfg, FishCVars.VisibilityLayerEnabled, OnEnabledChanged, true);
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

    /// <summary>Сколько сущностей скрыто прямо сейчас.</summary>
    public int HiddenCount => _managed.Count;

    private void OnEnabledChanged(bool value)
    {
        _enabled = value;

        if (!value)
        {
            // Выключение не должно оставлять мир в спрятанном состоянии: слой снимается,
            // обычные клиенты снова получают эти сущности.
            RevealAll();
        }
        else
        {
            _seen.Clear();
        }

        RefreshAdminMasks();
    }

    private void ParseList(string text)
    {
        _hidden.Clear();

        if (!string.IsNullOrWhiteSpace(text))
        {
            var parts = text.Split(new[] { ',', ';', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var id = part.Trim();
                if (id.Length > 0)
                    _hidden.Add(id);
            }
        }

        // Список поменяли на живом сервере — пересматриваем мир заново. Уже спрятанные
        // сущности сверяются в Reconcile: удалённые из списка прототипы возвращаются.
        _seen.Clear();
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        _seen.Clear();
        _managed.Clear();
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
    /// Кому показывать скрытые сущности: администраторам (в их теле и на камерах) и
    /// наблюдателям. Обычным игрокам бит не выдаётся.
    /// </summary>
    private void OnGetMask(ref GetVisMaskEvent ev)
    {
        if (!_enabled)
            return;

        if (HasComp<GhostComponent>(ev.Entity))
        {
            ev.VisibilityMask |= (int)VisibilityFlags.FishAdminOnly;
            return;
        }

        if (!_actorQuery.TryComp(ev.Entity, out var actor))
            return;

        if (_admins.GetAdminData(actor.PlayerSession) is not { Active: true })
            return;

        ev.VisibilityMask |= (int)VisibilityFlags.FishAdminOnly | (int)VisibilityFlags.Admin;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled)
        {
            if (_managed.Count > 0)
                RevealAll();

            return;
        }

        if (_hidden.Count > 0)
        {
            // Появляются и после старта раунда: спавн событий, разморозка, сборка карты.
            var query = AllEntityQuery<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var meta))
            {
                if (_seen.Contains(uid))
                    continue;

                // Сущность ещё не до конца на карте: в _seen её не записываем, иначе
                // следующий проход пропустил бы её и она осталась бы видимой до конца раунда.
                if (meta.EntityLifeStage < EntityLifeStage.MapInitialized)
                    continue;

                _seen.Add(uid);

                var prototype = meta.EntityPrototype;
                if (prototype == null || !_hidden.Contains(prototype.ID))
                    continue;

                Hide(uid, prototype.ID);
            }
        }

        Reconcile();

        // Права администратора могут выдать и снять посреди раунда.
        if (_timing.CurTime >= _nextMaskSweep)
        {
            _nextMaskSweep = _timing.CurTime + TimeSpan.FromSeconds(20);
            RefreshAdminMasks();
        }
    }

    /// <summary>
    /// Сверяет спрятанное со списком прототипов и с тем, что сделали другие системы
    /// видимости над теми же сущностями.
    /// </summary>
    private void Reconcile()
    {
        if (_managed.Count == 0)
            return;

        foreach (var (uid, entry) in _managed.ToArray())
        {
            if (Deleted(uid))
            {
                _managed.Remove(uid);
                continue;
            }

            // Прототип убрали из списка (или список опустел вовсе) — возвращаем как было.
            if (!_hidden.Contains(entry.Prototype))
            {
                Reveal(uid);
                continue;
            }

            // Тело получило разум: гост-роль, переселение, вселение. Прятать его нельзя.
            if (PlayerOwned(uid))
            {
                Reveal(uid);
                continue;
            }

            var vis = _visQuery.CompOrNull(uid);
            var current = vis?.Layer ?? (ushort)VisibilityFlags.Normal;

            if (current == (ushort)VisibilityFlags.Normal)
            {
                // Обычный слой — наш: накладываем ограничение поверх него.
                SetLayer(uid, vis, AdminOnlyLayer);
                continue;
            }

            // Слой принадлежит другой системе (покритие пола, призрак и так далее).
            // Спорить не нужно: сущность уже не уходит носителям Normal, пока чужие биты
            // стоят в её слое. Как только слой вернётся к Normal, ограничение наложится
            // снова на этом же проходе.
        }
    }

    /// <summary>
    /// Скрыть сущность от экипажа: её состояние перестаёт уходить обычным клиентам.
    /// </summary>
    /// <returns>false — прятать отказались.</returns>
    public bool Hide(EntityUid uid, string? prototype = null)
    {
        if (!_enabled || _managed.ContainsKey(uid))
            return false;

        // Игрока и тело с привязанным разумом скрывать нельзя: это ломает игру, а не чит.
        // Список прототипов правят на живом сервере, опечатка в нём стоит дорого.
        if (PlayerOwned(uid))
        {
            _sawmill.Warning($"Refused to hide {prototype ?? ToPrettyString(uid)}: belongs to a player.");
            return false;
        }

        var vis = _visQuery.CompOrNull(uid);
        var current = vis?.Layer ?? (ushort)VisibilityFlags.Normal;

        if (current != (ushort)VisibilityFlags.Normal)
        {
            // Слоем сейчас владеет другая система. Не записываем сущность в разобранные:
            // Reconcile вернётся к ней, когда слой станет обычным.
            _seen.Remove(uid);
            return false;
        }

        SetLayer(uid, vis, AdminOnlyLayer);
        _managed[uid] = (prototype ?? ToPrettyString(uid), current);
        return true;
    }

    /// <summary>Вернуть сущность обычным клиентам.</summary>
    public void Reveal(EntityUid uid)
    {
        if (!_managed.Remove(uid, out var entry))
            return;

        SetLayer(uid, _visQuery.CompOrNull(uid), entry.PreviousLayer);

        // Список прототипов могут вернуть целиком: сущность нужно разобрать заново.
        _seen.Remove(uid);
    }

    private void RevealAll()
    {
        foreach (var uid in _managed.Keys.ToArray())
            Reveal(uid);
    }

    private bool PlayerOwned(EntityUid uid)
    {
        return _actorQuery.HasComp(uid) ||
               _mindQuery.TryComp(uid, out var mind) && mind.HasMind;
    }

    private void SetLayer(EntityUid uid, VisibilityComponent? vis, ushort layer)
    {
        if (vis == null)
        {
            // Без компонента у сущности слой по умолчанию Normal. Добавляем компонент,
            // только когда реально что-то меняем, а не на каждый проход.
            if (layer == (ushort)VisibilityFlags.Normal)
                return;

            vis = EnsureComp<VisibilityComponent>(uid);
        }

        _visibility.SetLayer((uid, vis), layer);
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
