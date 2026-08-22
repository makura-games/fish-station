using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Arcade.Minesweeper;

/// <summary>
///     Уровень сложности сапёра. Определяет размер поля и количество мин.
/// </summary>
[Serializable, NetSerializable]
public enum MinesweeperDifficulty : byte
{
    Easy,
    Normal,
    Hard,
}

/// <summary>
///     Параметры игрового поля для конкретного уровня сложности.
/// </summary>
/// <param name="Width">Ширина поля в клетках.</param>
/// <param name="Height">Высота поля в клетках.</param>
/// <param name="Mines">Количество мин на поле.</param>
[Serializable, NetSerializable]
public readonly record struct MinesweeperBoardSettings(int Width, int Height, int Mines)
{
    /// <summary>
    ///     Общее число клеток поля.
    /// </summary>
    public int CellCount => Width * Height;
}

/// <summary>
///     Таблица параметров поля для всех уровней сложности сапёра.
/// </summary>
public static class MinesweeperDifficultySettings
{
    /// <summary>
    ///     Все уровни сложности в порядке возрастания. Используется для отрисовки переключателя в интерфейсе.
    /// </summary>
    public static readonly MinesweeperDifficulty[] All =
    {
        MinesweeperDifficulty.Easy,
        MinesweeperDifficulty.Normal,
        MinesweeperDifficulty.Hard,
    };

    private static readonly MinesweeperBoardSettings Easy = new(9, 9, 10);
    private static readonly MinesweeperBoardSettings Normal = new(12, 12, 25);
    private static readonly MinesweeperBoardSettings Hard = new(16, 16, 50);

    /// <summary>
    ///     Возвращает параметры поля для указанной сложности.
    /// </summary>
    public static MinesweeperBoardSettings Get(MinesweeperDifficulty difficulty)
    {
        return difficulty switch
        {
            MinesweeperDifficulty.Normal => Normal,
            MinesweeperDifficulty.Hard => Hard,
            _ => Easy,
        };
    }
}
