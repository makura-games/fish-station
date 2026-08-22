using System.Linq;
using Content.Shared._Fish.Arcade.Minesweeper;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Arcade.Minesweeper;

/// <summary>
///     Серверная логика консоли с сапёром: обрабатывает ввод из интерфейса,
///     ведёт таймер партии и рассылает состояние поля.
/// </summary>
public sealed partial class MinesweeperArcadeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializeScoreboard();

        SubscribeLocalEvent<MinesweeperArcadeComponent, ComponentInit>(OnComponentInit);

        Subs.BuiEvents<MinesweeperArcadeComponent>(MinesweeperUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
            subs.Event<MinesweeperRevealMessage>(OnReveal);
            subs.Event<MinesweeperFlagMessage>(OnFlag);
            subs.Event<MinesweeperNewGameMessage>(OnNewGame);
        });
    }

    private void OnComponentInit(Entity<MinesweeperArcadeComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Game = new MinesweeperGame(ent.Comp.DefaultDifficulty);
    }

    private void OnUiOpened(Entity<MinesweeperArcadeComponent> ent, ref BoundUIOpenedEvent args)
    {
        // Первый открывший консоль становится игроком, остальные смотрят.
        ent.Comp.Player ??= args.Actor;

        UpdateUi(ent);
    }

    private void OnUiClosed(Entity<MinesweeperArcadeComponent> ent, ref BoundUIClosedEvent args)
    {
        var actor = args.Actor;
        if (ent.Comp.Player != actor)
            return;

        // Игрок ушёл — консоль переходит первому оставшемуся зрителю.
        ent.Comp.Player = _ui.GetActors(ent.Owner, MinesweeperUiKey.Key)
            .Cast<EntityUid?>()
            .FirstOrDefault(other => other != actor);

        UpdateUi(ent);
    }

    private void OnReveal(Entity<MinesweeperArcadeComponent> ent, ref MinesweeperRevealMessage args)
    {
        TryReveal(ent, args.X, args.Y, args.Actor);
    }

    private void OnFlag(Entity<MinesweeperArcadeComponent> ent, ref MinesweeperFlagMessage args)
    {
        TryToggleFlag(ent, args.X, args.Y, args.Actor);
    }

    private void OnNewGame(Entity<MinesweeperArcadeComponent> ent, ref MinesweeperNewGameMessage args)
    {
        TryStartNewGame(ent, args.Difficulty, args.Actor);
    }

    /// <summary>
    ///     Открывает клетку от лица указанного игрока.
    /// </summary>
    /// <returns>True, если ход состоялся.</returns>
    public bool TryReveal(Entity<MinesweeperArcadeComponent> ent, int x, int y, EntityUid user)
    {
        if (!CanPlay(ent, user) || ent.Comp.Game is not { } game)
            return false;

        var wasReady = game.Status == MinesweeperStatus.Ready;

        if (!game.TryReveal(x, y, _random))
            return false;

        // Таймер стартует с первого реально сделанного хода, а не с открытия интерфейса.
        if (wasReady && game.Status != MinesweeperStatus.Ready)
            ent.Comp.StartTime = _timing.CurTime;

        FinishTurn(ent, game, user);
        return true;
    }

    /// <summary>
    ///     Ставит или снимает флажок от лица указанного игрока.
    /// </summary>
    /// <returns>True, если ход состоялся.</returns>
    public bool TryToggleFlag(Entity<MinesweeperArcadeComponent> ent, int x, int y, EntityUid user)
    {
        if (!CanPlay(ent, user) || ent.Comp.Game is not { } game)
            return false;

        if (!game.TryToggleFlag(x, y))
            return false;

        FinishTurn(ent, game, user);
        return true;
    }

    /// <summary>
    ///     Начинает новую партию на выбранной сложности.
    /// </summary>
    /// <returns>True, если партия создана.</returns>
    public bool TryStartNewGame(Entity<MinesweeperArcadeComponent> ent, MinesweeperDifficulty difficulty, EntityUid user)
    {
        if (!CanPlay(ent, user))
            return false;

        ent.Comp.Game = new MinesweeperGame(difficulty);
        ent.Comp.StartTime = null;
        ent.Comp.EndTime = null;

        UpdateUi(ent);
        return true;
    }

    /// <summary>
    ///     Ходить может только тот, кто сейчас держит консоль за игрока.
    /// </summary>
    public bool CanPlay(Entity<MinesweeperArcadeComponent> ent, EntityUid user)
    {
        return ent.Comp.Player == user;
    }

    /// <summary>
    ///     Закрывает ход: фиксирует время окончания, записывает рекорд при победе и обновляет интерфейс.
    /// </summary>
    private void FinishTurn(Entity<MinesweeperArcadeComponent> ent, MinesweeperGame game, EntityUid user)
    {
        if (game.Status is MinesweeperStatus.Won or MinesweeperStatus.Lost && ent.Comp.EndTime == null)
        {
            ent.Comp.EndTime = _timing.CurTime;

            // Подрыв на мине озвучиваем один раз, ровно в момент проигрыша.
            if (game.Status == MinesweeperStatus.Lost)
                _audio.PlayPvs(ent.Comp.ExplosionSound, ent.Owner);

            if (game.Status == MinesweeperStatus.Won && ent.Comp.StartTime is { } startTime)
                TryRegisterScore(Name(user), game.Difficulty, ent.Comp.EndTime.Value - startTime, out _);
        }

        UpdateUi(ent);
    }

    /// <summary>
    ///     Рассылает снимок поля всем, кто держит интерфейс открытым.
    /// </summary>
    private void UpdateUi(Entity<MinesweeperArcadeComponent> ent)
    {
        if (ent.Comp.Game is not { } game)
            return;

        if (!_ui.IsUiOpen(ent.Owner, MinesweeperUiKey.Key))
            return;

        var state = new MinesweeperUiState(
            game.Difficulty,
            game.Width,
            game.Height,
            game.GetBoard(),
            game.MinesRemaining,
            game.Status,
            ent.Comp.StartTime,
            ent.Comp.EndTime,
            GetNetEntity(ent.Comp.Player),
            GetScores());

        _ui.SetUiState(ent.Owner, MinesweeperUiKey.Key, state);
    }
}
