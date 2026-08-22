using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Arcade.Minesweeper;

/// <summary>
///     Видимое состояние одной клетки поля.
/// </summary>
[Serializable, NetSerializable]
public enum MinesweeperCell : byte
{
    /// <summary>
    ///     Закрытая клетка.
    /// </summary>
    Hidden,

    /// <summary>
    ///     Закрытая клетка, помеченная флажком.
    /// </summary>
    Flagged,

    /// <summary>
    ///     Флажок, поставленный не на мину. Показывается только после проигрыша.
    /// </summary>
    FlagWrong,

    /// <summary>
    ///     Мина, открытая после проигрыша.
    /// </summary>
    Mine,

    /// <summary>
    ///     Мина, на которой игрок подорвался.
    /// </summary>
    MineExploded,

    /// <summary>
    ///     Открытая клетка без мин по соседству. Дальше идут значения с числом соседних мин.
    /// </summary>
    Empty,

    Adjacent1,
    Adjacent2,
    Adjacent3,
    Adjacent4,
    Adjacent5,
    Adjacent6,
    Adjacent7,
    Adjacent8,
}

/// <summary>
///     Состояние партии сапёра.
/// </summary>
[Serializable, NetSerializable]
public enum MinesweeperStatus : byte
{
    /// <summary>
    ///     Поле создано, но первый ход ещё не сделан
    /// </summary>
    Ready,

    /// <summary>
    ///     Партия идёт.
    /// </summary>
    Playing,

    /// <summary>
    ///     Все безопасные клетки открыты.
    /// </summary>
    Won,

    /// <summary>
    ///     Игрок открыл мину.
    /// </summary>
    Lost,
}

/// <summary>
///     Хелперы для <see cref="MinesweeperCell"/>.
/// </summary>
public static class MinesweeperCellExt
{
    /// <summary>
    ///     Возвращает клетку, отображающую указанное число соседних мин.
    /// </summary>
    public static MinesweeperCell FromAdjacent(int adjacent)
    {
        return (MinesweeperCell) ((int) MinesweeperCell.Empty + Math.Clamp(adjacent, 0, 8));
    }

    /// <summary>
    ///     Число соседних мин для открытой клетки, либо <c>null</c>, если клетка не открыта.
    /// </summary>
    public static int? GetAdjacent(this MinesweeperCell cell)
    {
        if (cell < MinesweeperCell.Empty)
            return null;

        return cell - MinesweeperCell.Empty;
    }
}
