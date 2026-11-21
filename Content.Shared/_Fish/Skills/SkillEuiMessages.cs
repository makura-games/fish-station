using Robust.Shared.Serialization;
using Content.Shared.Eui;

namespace Content.Shared._Fish.Skills;

[Serializable, NetSerializable]
public sealed class SkillEuiState : EuiStateBase
{
    public int RemainingPoints;
    public int TotalPoints;
    public bool Locked;
    public string? JobName;
    public string? StatusMessage;
    public List<SkillUiEntry> Skills = new();

    public SkillEuiState(
        int remainingPoints,
        int totalPoints,
        bool locked,
        string? jobName,
        string? statusMessage,
        List<SkillUiEntry> skills)
    {
        RemainingPoints = remainingPoints;
        TotalPoints = totalPoints;
        Locked = locked;
        JobName = jobName;
        StatusMessage = statusMessage;
        Skills = skills;
    }
}

[Serializable, NetSerializable]
public sealed class SkillEuiSetLevelMessage : EuiMessageBase
{
    public string SkillId = string.Empty;

    /// <summary>
    /// Шаг относительно актуального серверного уровня (+1 / −1).
    /// Клиент не шлёт абсолютный уровень из устаревшего UI-состояния.
    /// </summary>
    public int Delta;

    public SkillEuiSetLevelMessage(string skillId, int delta)
    {
        SkillId = skillId;
        Delta = delta;
    }

    public SkillEuiSetLevelMessage()
    {
    }
}

[Serializable, NetSerializable]
public sealed class SkillEuiConfirmMessage : EuiMessageBase;

[Serializable, NetSerializable]
public sealed class SkillEuiResetMessage : EuiMessageBase;
