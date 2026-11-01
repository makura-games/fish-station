namespace Content.Server._Fish.Research.Bluespace;

/// <summary>Stores the shared bluespace background and safety hysteresis on a station entity.</summary>
[RegisterComponent]
[Access(typeof(FishBluespaceBackgroundSystem), typeof(FishBluespaceResearchSystem))]
public sealed partial class FishBluespaceBackgroundComponent : Component
{
    /// <summary>Background removed per unpaused second without machinery.</summary>
    [DataField]
    public float DecayPerSecond = 0.03f;

    /// <summary>First station warning threshold, in the range zero to one hundred.</summary>
    [DataField]
    public float WarningThreshold = 60f;

    /// <summary>Severe instability warning threshold.</summary>
    [DataField]
    public float DangerThreshold = 85f;

    /// <summary>Background at which risky station machinery is scrammed.</summary>
    [DataField]
    public float CriticalThreshold = 95f;

    /// <summary>Background must fall strictly below this level to clear the station scram.</summary>
    [DataField]
    public float RecoveryThreshold = 80f;

    /// <summary>Time before previously announced warning severities can rearm; escalation bypasses this delay.</summary>
    [DataField]
    public TimeSpan WarningCooldown = TimeSpan.FromSeconds(60);

    [ViewVariables(VVAccess.ReadOnly)]
    internal float Level;

    internal bool Critical;
    internal float WarningCooldownRemaining;
    internal int LastWarningBand;
    internal int ActiveProbers;
}
