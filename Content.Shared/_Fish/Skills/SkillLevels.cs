namespace Content.Shared._Fish.Skills;

/// <summary>
/// Уровни навыков Fish Station (1..5).
/// </summary>
public static class SkillLevels
{
    public const int Unskilled = 1;
    public const int Basic = 2;
    public const int Trained = 3;
    public const int Experienced = 4;
    public const int Master = 5;

    public const int Min = Unskilled;
    public const int Max = Master;

    /// <summary>Базовый уровень для расчёта множителя времени.</summary>
    public const int Baseline = Trained;

    public const int DifficultyEasy = Unskilled;
    public const int DifficultyAverage = Basic;
    public const int DifficultyHard = Experienced;

    public static string GetLevelNameKey(int level) => level switch
    {
        Unskilled => "skills-level-unskilled",
        Basic => "skills-level-basic",
        Trained => "skills-level-trained",
        Experienced => "skills-level-experienced",
        Master => "skills-level-master",
        _ => "skills-level-unknown",
    };
}
