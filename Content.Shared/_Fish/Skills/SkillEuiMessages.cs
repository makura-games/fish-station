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
    public int Level;

    public SkillEuiSetLevelMessage(string skillId, int level)
    {
        SkillId = skillId;
        Level = level;
    }

    public SkillEuiSetLevelMessage()
    {
    }
}

[Serializable, NetSerializable]
public sealed class SkillEuiConfirmMessage : EuiMessageBase;

[Serializable, NetSerializable]
public sealed class SkillEuiResetMessage : EuiMessageBase;
