using Robust.Shared.GameObjects;

namespace Content.Server._Fish.MentalHealth;

[RegisterComponent]
public sealed partial class MentalHealthExposureComponent : Component
{
    public Dictionary<string, MentalHealthSubstanceState> Substances = new();
}

public sealed class MentalHealthSubstanceState
{
    public int Doses;
    public float Addiction;
    public float TolerancePenalty;
    public TimeSpan LastDose;
    public TimeSpan LastToleranceDecay;
    public TimeSpan LastAddictionDecay;
    public TimeSpan NextWithdrawal;
}
