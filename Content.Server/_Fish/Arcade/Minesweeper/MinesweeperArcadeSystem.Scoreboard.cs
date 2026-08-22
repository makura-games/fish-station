using System.Linq;
using Content.Shared._Fish.Arcade.Minesweeper;
using Content.Shared.GameTicking;

namespace Content.Server._Fish.Arcade.Minesweeper;

/// <summary>
///     Таблица рекордов сапёра. Общая на всю станцию и на весь раунд:
///     любая консоль показывает одни и те же лучшие результаты.
/// </summary>
public sealed partial class MinesweeperArcadeSystem
{
    /// <summary>
    ///     Сколько результатов хранится на каждую сложность.
    /// </summary>
    private const int MaxScoresPerDifficulty = 5;

    private readonly List<MinesweeperScoreEntry> _scores = new();

    private void InitializeScoreboard()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _scores.Clear();
    }

    /// <summary>
    ///     Возвращает копию таблицы рекордов, отсортированную по сложности и времени.
    /// </summary>
    public List<MinesweeperScoreEntry> GetScores()
    {
        var result = new List<MinesweeperScoreEntry>(_scores);
        result.Sort(CompareScores);
        return result;
    }

    /// <summary>
    ///     Пытается добавить результат в таблицу рекордов.
    /// </summary>
    /// <param name="placement">Занятое место, начиная с первого.</param>
    /// <returns>True, если результат попал в таблицу.</returns>
    public bool TryRegisterScore(string name, MinesweeperDifficulty difficulty, TimeSpan time, out int placement)
    {
        placement = 0;

        var board = _scores.Where(entry => entry.Difficulty == difficulty).ToList();
        board.Sort(CompareScores);

        // Таблица заполнена и результат не лучше худшего в ней.
        if (board.Count >= MaxScoresPerDifficulty && board[^1].Time <= time)
            return false;

        var entry = new MinesweeperScoreEntry(name, difficulty, time);
        _scores.Add(entry);
        board.Add(entry);
        board.Sort(CompareScores);

        while (board.Count > MaxScoresPerDifficulty)
        {
            var worst = board[^1];
            board.RemoveAt(board.Count - 1);
            _scores.Remove(worst);
        }

        placement = board.IndexOf(entry) + 1;
        return true;
    }

    private static int CompareScores(MinesweeperScoreEntry first, MinesweeperScoreEntry second)
    {
        var byDifficulty = first.Difficulty.CompareTo(second.Difficulty);
        return byDifficulty != 0 ? byDifficulty : first.Time.CompareTo(second.Time);
    }
}
