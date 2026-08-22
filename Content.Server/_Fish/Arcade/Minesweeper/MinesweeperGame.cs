using Content.Shared._Fish.Arcade.Minesweeper;
using Robust.Shared.Random;

namespace Content.Server._Fish.Arcade.Minesweeper;

/// <summary>
///     Партия сапёра: расстановка мин, вскрытие клеток и определение победы.
///     Класс намеренно не знает ничего про ECS и сеть — этим занимается <see cref="MinesweeperArcadeSystem"/>.
/// </summary>
public sealed class MinesweeperGame
{
    /// <summary>
    ///     Максимальное число соседей у клетки. Размер буфера для <see cref="GetNeighbours"/>.
    /// </summary>
    private const int MaxNeighbours = 8;

    /// <summary>
    ///     Сложность, на которой создана партия.
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
    ///     Итоговое число мин на поле.
    /// </summary>
    public readonly int MineCount;

    private readonly bool[] _mines;
    private readonly bool[] _revealed;
    private readonly bool[] _flagged;
    private readonly int[] _adjacent;

    /// <summary>
    ///     Клетка, на которой игрок подорвался, либо -1.
    /// </summary>
    private int _explodedIndex = -1;

    /// <summary>
    ///     Сколько безопасных клеток уже открыто.
    /// </summary>
    private int _revealedCount;

    /// <summary>
    ///     Состояние партии.
    /// </summary>
    public MinesweeperStatus Status { get; private set; } = MinesweeperStatus.Ready;

    /// <summary>
    ///     Сколько флажков сейчас стоит на поле.
    /// </summary>
    public int FlagCount { get; private set; }

    /// <summary>
    ///     Сколько мин осталось по мнению игрока.
    /// </summary>
    public int MinesRemaining => MineCount - FlagCount;

    /// <summary>
    ///     Общее число клеток поля.
    /// </summary>
    private int CellCount => Width * Height;

    public MinesweeperGame(MinesweeperDifficulty difficulty)
    {
        var settings = MinesweeperDifficultySettings.Get(difficulty);

        Difficulty = difficulty;
        Width = settings.Width;
        Height = settings.Height;

        MineCount = Math.Clamp(settings.Mines, 1, Math.Max(1, settings.CellCount - 9));

        _mines = new bool[CellCount];
        _revealed = new bool[CellCount];
        _flagged = new bool[CellCount];
        _adjacent = new int[CellCount];
    }

    /// <summary>
    ///     Пытается открыть клетку. Клик по уже открытой цифре работает как аккорд:
    ///     если вокруг стоит ровно столько флажков, сколько показывает цифра, открываются все остальные соседи.
    /// </summary>
    /// <returns>True, если состояние поля изменилось.</returns>
    public bool TryReveal(int x, int y, IRobustRandom random)
    {
        if (!CanReveal(x, y, out var index))
            return false;

        if (Status == MinesweeperStatus.Ready)
        {
            GenerateMines(index, random);
            Status = MinesweeperStatus.Playing;
        }

        return _revealed[index] ? Chord(index) : Reveal(index);
    }

    /// <summary>
    ///     Проверяет, можно ли вообще трогать указанную клетку.
    /// </summary>
    public bool CanReveal(int x, int y, out int index)
    {
        index = -1;

        if (Status != MinesweeperStatus.Ready && Status != MinesweeperStatus.Playing)
            return false;

        if (!TryGetIndex(x, y, out index))
            return false;

        // Флажок специально блокирует случайное вскрытие.
        return !_flagged[index];
    }

    /// <summary>
    ///     Пытается поставить или снять флажок на закрытой клетке.
    /// </summary>
    /// <returns>True, если состояние поля изменилось.</returns>
    public bool TryToggleFlag(int x, int y)
    {
        if (!CanToggleFlag(x, y, out var index))
            return false;

        ToggleFlag(index);
        return true;
    }

    /// <summary>
    ///     Проверяет, можно ли переключить флажок на указанной клетке.
    /// </summary>
    public bool CanToggleFlag(int x, int y, out int index)
    {
        index = -1;

        if (Status != MinesweeperStatus.Ready && Status != MinesweeperStatus.Playing)
            return false;

        if (!TryGetIndex(x, y, out index))
            return false;

        if (_revealed[index])
            return false;

        // Флажков не может быть больше, чем мин: иначе счётчик уходит в минус.
        return _flagged[index] || FlagCount < MineCount;
    }

    /// <summary>
    ///     Собирает видимое состояние поля для отправки на клиент.
    ///     Нераскрытые мины наружу не попадают, пока партия не проиграна.
    /// </summary>
    public MinesweeperCell[] GetBoard()
    {
        var board = new MinesweeperCell[CellCount];
        var lost = Status == MinesweeperStatus.Lost;

        for (var index = 0; index < board.Length; index++)
        {
            if (_revealed[index])
            {
                board[index] = MinesweeperCellExt.FromAdjacent(_adjacent[index]);
                continue;
            }

            if (_flagged[index])
            {
                board[index] = lost && !_mines[index] ? MinesweeperCell.FlagWrong : MinesweeperCell.Flagged;
                continue;
            }

            if (lost && _mines[index])
            {
                board[index] = index == _explodedIndex ? MinesweeperCell.MineExploded : MinesweeperCell.Mine;
                continue;
            }

            board[index] = MinesweeperCell.Hidden;
        }

        return board;
    }

    /// <summary>
    ///     Есть ли мина в указанной клетке. Серверный запрос: наружу эти данные не уходят,
    ///     пока партия не проиграна.
    /// </summary>
    public bool IsMine(int x, int y)
    {
        return TryGetIndex(x, y, out var index) && _mines[index];
    }

    private void ToggleFlag(int index)
    {
        _flagged[index] = !_flagged[index];
        FlagCount += _flagged[index] ? 1 : -1;
    }

    /// <summary>
    ///     Открывает клетку и, если вокруг неё нет мин, каскадом раскрывает соседнюю пустую область.
    /// </summary>
    private bool Reveal(int startIndex)
    {
        if (_mines[startIndex])
        {
            _explodedIndex = startIndex;
            Status = MinesweeperStatus.Lost;
            return true;
        }

        Span<int> neighbours = stackalloc int[MaxNeighbours];

        _revealed[startIndex] = true;
        _revealedCount++;

        var queue = new Queue<int>();
        queue.Enqueue(startIndex);

        while (queue.TryDequeue(out var index))
        {
            if (_adjacent[index] != 0)
                continue;

            var count = GetNeighbours(index, neighbours);
            for (var i = 0; i < count; i++)
            {
                var neighbour = neighbours[i];
                if (_revealed[neighbour] || _flagged[neighbour] || _mines[neighbour])
                    continue;

                _revealed[neighbour] = true;
                _revealedCount++;
                queue.Enqueue(neighbour);
            }
        }

        CheckWin();
        return true;
    }

    /// <summary>
    ///     Аккорд: открывает всех незафлаженных соседей открытой цифры,
    ///     если игрок уже расставил вокруг неё нужное число флажков.
    /// </summary>
    private bool Chord(int index)
    {
        if (_adjacent[index] == 0)
            return false;

        Span<int> neighbours = stackalloc int[MaxNeighbours];
        var count = GetNeighbours(index, neighbours);

        var flags = 0;
        for (var i = 0; i < count; i++)
        {
            if (_flagged[neighbours[i]])
                flags++;
        }

        if (flags != _adjacent[index])
            return false;

        var changed = false;
        for (var i = 0; i < count; i++)
        {
            var neighbour = neighbours[i];
            if (_revealed[neighbour] || _flagged[neighbour])
                continue;

            changed |= Reveal(neighbour);

            // Подрыв на аккорде сразу заканчивает партию, дальше открывать нечего.
            if (Status != MinesweeperStatus.Playing)
                break;
        }

        return changed;
    }

    /// <summary>
    ///     Расставляет мины так, чтобы клетка первого хода и её соседи остались пустыми.
    /// </summary>
    private void GenerateMines(int safeIndex, IRobustRandom random)
    {
        var safeX = safeIndex % Width;
        var safeY = safeIndex / Width;

        var candidates = new List<int>(CellCount);
        for (var index = 0; index < CellCount; index++)
        {
            var x = index % Width;
            var y = index / Width;

            if (Math.Abs(x - safeX) <= 1 && Math.Abs(y - safeY) <= 1)
                continue;

            candidates.Add(index);
        }

        random.Shuffle(candidates);

        var mines = Math.Min(MineCount, candidates.Count);
        for (var i = 0; i < mines; i++)
        {
            _mines[candidates[i]] = true;
        }

        Span<int> neighbours = stackalloc int[MaxNeighbours];
        for (var index = 0; index < CellCount; index++)
        {
            if (_mines[index])
                continue;

            var count = GetNeighbours(index, neighbours);
            var adjacent = 0;
            for (var i = 0; i < count; i++)
            {
                if (_mines[neighbours[i]])
                    adjacent++;
            }

            _adjacent[index] = adjacent;
        }
    }

    /// <summary>
    ///     Помечает партию выигранной, когда открыты все безопасные клетки.
    ///     Оставшиеся мины автоматически получают флажки, чтобы поле выглядело завершённым.
    /// </summary>
    private void CheckWin()
    {
        if (_revealedCount < CellCount - MineCount)
            return;

        Status = MinesweeperStatus.Won;

        for (var index = 0; index < CellCount; index++)
        {
            if (!_mines[index] || _flagged[index])
                continue;

            ToggleFlag(index);
        }
    }

    /// <summary>
    ///     Записывает индексы соседних клеток в буфер и возвращает их количество.
    /// </summary>
    private int GetNeighbours(int index, Span<int> buffer)
    {
        var x = index % Width;
        var y = index / Width;
        var count = 0;

        for (var dy = -1; dy <= 1; dy++)
        {
            var ny = y + dy;
            if (ny < 0 || ny >= Height)
                continue;

            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                var nx = x + dx;
                if (nx < 0 || nx >= Width)
                    continue;

                buffer[count++] = ny * Width + nx;
            }
        }

        return count;
    }

    private bool TryGetIndex(int x, int y, out int index)
    {
        index = -1;

        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return false;

        index = y * Width + x;
        return true;
    }
}
