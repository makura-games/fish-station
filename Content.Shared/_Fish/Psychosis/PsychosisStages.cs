namespace Content.Shared._Fish.Psychosis;

/// <summary>
/// Стадии психоза. Используются и сервером, и клиентом, чтобы пороги не дублировались.
/// </summary>
public enum PsychosisStage
{
    /// <summary>
    /// Психоза нет.
    /// </summary>
    Calm = 0,

    /// <summary>
    /// Лёгкая тревога, эффектов ещё нет.
    /// </summary>
    Unsettled = 1,

    /// <summary>
    /// Искажение картинки и звука, усиленная резкость.
    /// </summary>
    Distorted = 2,

    /// <summary>
    /// Искажение интерфейса и речи.
    /// </summary>
    Delirious = 3,

    /// <summary>
    /// Предельная стадия.
    /// </summary>
    Terminal = 4,
}

/// <summary>
/// Общие пороги стадий психоза.
/// </summary>
public static class PsychosisStages
{
    public const float UnsettledMin = 33f;
    public const float DistortedMin = 50f;
    public const float DeliriousMin = 66f;
    public const float TerminalMin = 85f;

    public static PsychosisStage Get(float level)
    {
        return level switch
        {
            >= TerminalMin => PsychosisStage.Terminal,
            >= DeliriousMin => PsychosisStage.Delirious,
            >= DistortedMin => PsychosisStage.Distorted,
            >= UnsettledMin => PsychosisStage.Unsettled,
            _ => PsychosisStage.Calm,
        };
    }
}
