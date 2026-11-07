using Content.Shared._Fish.Xenobiology;
using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Database;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Xenobiology;

/// <summary>Запускает ограниченные по времени и населению реакции лабораторных экстрактов.</summary>
public sealed partial class FishXenobiologyReactionSystem : EntitySystem
{
    public const int SummonLimit = 12;

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private EntityQuery<TransformComponent> _transformQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishXenobiologyExtractComponent, SolutionTransferredEvent>(OnTransferred);
    }

    /// <summary>Новое изменение раствора не может присвоить уже начавшуюся реакцию другому переносу.</summary>
    public void InvalidateTransfer(EntityUid extract)
    {
        if (TryComp<FishXenobiologyReactionComponent>(extract, out var reaction))
            reaction.AwaitingTransfer = false;
    }

    private void OnTransferred(Entity<FishXenobiologyExtractComponent> ent, ref SolutionTransferredEvent args)
    {
        // Fish-edit: заливка сообщает об инициаторе после вложенного SolutionChangedEvent.
        if (args.To == ent.Owner && args.Amount > 0 &&
            TryComp<FishXenobiologyReactionComponent>(ent, out var reaction) &&
            reaction.StartedTick == _timing.CurTick && reaction.AwaitingTransfer)
        {
            reaction.User = args.User;
            reaction.AwaitingTransfer = false;
        }
    }

    /// <summary>После расходования катализатора принимаем подтверждение только текущего переноса.</summary>
    public void ExpectTransfer(EntityUid extract)
    {
        if (TryComp<FishXenobiologyReactionComponent>(extract, out var reaction))
            reaction.AwaitingTransfer = true;
    }

    /// <summary>Проверяет координаты и места для призыва, не расходуя реагенты.</summary>
    public bool CanStart(EntityUid extract, FishXenobiologyReactionPrototype recipe)
    {
        if (recipe.Effect == FishXenobiologyReactionEffect.None)
            return true;

        if (TerminatingOrDeleted(extract) || HasComp<FishXenobiologyReactionComponent>(extract) ||
            !_transformQuery.TryComp(extract, out var transform) || transform.MapUid == null ||
            !_transform.TryGetMapOrGridCoordinates(extract, out _) ||
            recipe.Delay <= TimeSpan.Zero || !float.IsFinite(recipe.Radius) || recipe.Radius <= 0 ||
            !float.IsFinite(recipe.Strength) || recipe.Strength < 0)
            return false;

        return recipe.Effect != FishXenobiologyReactionEffect.Spawn ||
               recipe.SpawnCount is > 0 and <= SummonLimit && recipe.Creatures.Count > 0 &&
               TryGetFloor(extract, out _) &&
               CountSummons(transform.MapUid.Value) + recipe.SpawnCount <= SummonLimit;
    }

    /// <summary>Резервирует один эффект; ожидающие призывы сразу занимают места в лимите.</summary>
    public bool TryStart(Entity<FishXenobiologyExtractComponent> ent, FishXenobiologyReactionPrototype recipe)
    {
        if (!CanStart(ent, recipe))
            return false;
        if (recipe.Effect == FishXenobiologyReactionEffect.None)
            return true;

        var pending = AddComp<FishXenobiologyReactionComponent>(ent);
        pending.Recipe = recipe.ID;
        pending.StartedTick = _timing.CurTick;
        pending.FinishAt = _timing.CurTime + recipe.Delay;
        _popup.PopupEntity(Loc.GetString("fish-xeno-reaction-warning", ("seconds", recipe.Delay.TotalSeconds)), ent);
        return true;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<FishXenobiologyReactionComponent>();
        while (query.MoveNext(out var uid, out var pending))
        {
            if (_timing.CurTime < pending.FinishAt)
                continue;

            // Fish-edit: снимаем резерв до спавнов и событий, чтобы исключить повторное срабатывание.
            RemComp<FishXenobiologyReactionComponent>(uid);
            if (TerminatingOrDeleted(uid) || !_prototype.TryIndex(pending.Recipe, out var recipe) ||
                !_transform.TryGetMapOrGridCoordinates(uid, out _))
                continue;

            var user = pending.User is { } initiator && !TerminatingOrDeleted(initiator) ? pending.User : null;
            var coordinates = _transform.GetMapCoordinates(uid);
            _adminLog.Add(LogType.Action, LogImpact.High,
                $"Xenobiology reaction {recipe.ID} from {ToPrettyString(uid)} at {coordinates:coordinates}; initiator {(user is { } actor ? ToPrettyString(actor) : "unknown")}.");
            ApplyEffect(uid, recipe, coordinates, user);
        }
    }

    private int CountSummons(EntityUid map)
    {
        var count = 0;
        var creatures = EntityQueryEnumerator<FishXenobiologySummonedComponent, TransformComponent>();
        while (creatures.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.MapUid == map && !TerminatingOrDeleted(uid) && !_mobState.IsDead(uid))
                count++;
        }

        var pending = EntityQueryEnumerator<FishXenobiologyReactionComponent, TransformComponent>();
        while (pending.MoveNext(out var uid, out var reaction, out var transform))
        {
            if (transform.MapUid == map && !TerminatingOrDeleted(uid) &&
                _prototype.TryIndex(reaction.Recipe, out var recipe) && recipe.Effect == FishXenobiologyReactionEffect.Spawn)
                count += recipe.SpawnCount;
        }
        return count;
    }

    private bool TryGetFloor(EntityUid extract, out EntityCoordinates coordinates)
    {
        coordinates = default;
        if (!_transform.TryGetMapOrGridCoordinates(extract, out var position) ||
            _turf.GetTileRef(position.Value) is not { } tile || _turf.IsSpace(tile))
            return false;
        coordinates = position.Value;
        return true;
    }

    private void Summon(EntityUid source, FishXenobiologyReactionPrototype recipe)
    {
        if (!TryGetFloor(source, out var coordinates) || Transform(source).MapUid is not { } map ||
            CountSummons(map) + recipe.SpawnCount > SummonLimit)
        {
            _popup.PopupEntity(Loc.GetString("fish-xeno-reaction-summon-blocked"), source);
            return;
        }

        for (var i = 0; i < recipe.SpawnCount; i++)
        {
            var creature = Spawn(_random.Pick(recipe.Creatures), coordinates);
            AddComp<FishXenobiologySummonedComponent>(creature);
        }
    }
}
