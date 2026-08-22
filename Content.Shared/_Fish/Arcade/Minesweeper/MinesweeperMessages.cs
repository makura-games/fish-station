using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Arcade.Minesweeper;

/// <summary>
///     Полный снимок состояния консоли с сапёром.
/// </summary>
[Serializable, NetSerializable]
public sealed class MinesweeperUiState : BoundUserInterfaceState
{
    /// <summary>
    ///     Сложность текущей партии.
    /// </summary>
    public readonly MinesweeperDifficulty Difficulty;

    /// <summary>
    ///     Ширина поля в клетках.
    /// </summary>
    public readonly int Width;

    /// <summary>
    ///     Высота поля в клетках.
    /// </summary>
    public readonly int Height;

    /// <summary>
    ///     Видимое состояние всех клеток, индекс клетки равен <c>y * Width + x</c>.
    /// </summary>
    public readonly MinesweeperCell[] Board;

    /// <summary>
    ///     Сколько мин осталось по мнению игрока: общее число мин минус число поставленных флажков.
    /// </summary>
    public readonly int MinesRemaining;

    /// <summary>
    ///     Состояние партии.
    /// </summary>
    public readonly MinesweeperStatus Status;

    /// <summary>
    ///     Момент первого хода по серверному времени, либо <c>null</c>, если партия ещё не начата.
    /// </summary>
    public readonly TimeSpan? StartTime;

    /// <summary>
    ///     Момент завершения партии по серверному времени, либо <c>null</c>, если партия ещё идёт.
    /// </summary>
    public readonly TimeSpan? EndTime;

    /// <summary>
    ///     Кто сейчас играет. Остальные зрители видят поле, но не могут ходить.
    /// </summary>
    public readonly NetEntity? Player;

    /// <summary>
    ///     Таблица рекордов по всем сложностям.
    /// </summary>
    public readonly List<MinesweeperScoreEntry> Scores;

    public MinesweeperUiState(
        MinesweeperDifficulty difficulty,
        int width,
        int height,
        MinesweeperCell[] board,
        int minesRemaining,
        MinesweeperStatus status,
        TimeSpan? startTime,
        TimeSpan? endTime,
        NetEntity? player,
        List<MinesweeperScoreEntry> scores)
    {
        Difficulty = difficulty;
        Width = width;
        Height = height;
        Board = board;
        MinesRemaining = minesRemaining;
        Status = status;
        StartTime = startTime;
        EndTime = endTime;
        Player = player;
        Scores = scores;
    }
}

/// <summary>
///     Одна запись таблицы рекордов: кто и за сколько прошёл уровень.
/// </summary>
[Serializable, NetSerializable]
public sealed class MinesweeperScoreEntry
{
    /// <summary>
    ///     Имя персонажа, прошедшего уровень.
    /// </summary>
    public readonly string Name;

    /// <summary>
    ///     Сложность, на которой поставлен рекорд.
    /// </summary>
    public readonly MinesweeperDifficulty Difficulty;

    /// <summary>
    ///     Затраченное время.
    /// </summary>
    public readonly TimeSpan Time;

    public MinesweeperScoreEntry(string name, MinesweeperDifficulty difficulty, TimeSpan time)
    {
        Name = name;
        Difficulty = difficulty;
        Time = time;
    }
}

/// <summary>
///     Игрок открывает клетку. Клик по уже открытой цифре работает как аккорд.
/// </summary>
[Serializable, NetSerializable]
public sealed class MinesweeperRevealMessage : BoundUserInterfaceMessage
{
    public readonly int X;
    public readonly int Y;

    public MinesweeperRevealMessage(int x, int y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
///     Игрок ставит или снимает флажок на закрытой клетке.
/// </summary>
[Serializable, NetSerializable]
public sealed class MinesweeperFlagMessage : BoundUserInterfaceMessage
{
    public readonly int X;
    public readonly int Y;

    public MinesweeperFlagMessage(int x, int y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
///     Игрок начинает новую партию на выбранной сложности.
/// </summary>
[Serializable, NetSerializable]
public sealed class MinesweeperNewGameMessage : BoundUserInterfaceMessage
{
    public readonly MinesweeperDifficulty Difficulty;

    public MinesweeperNewGameMessage(MinesweeperDifficulty difficulty)
    {
        Difficulty = difficulty;
    }
}
