namespace Content.Server._Fish.Research.Bluespace;

/// <summary>Configures an APC-powered suppressor that remains usable during a station scram.</summary>
[RegisterComponent, Access(typeof(FishBluespaceResearchSystem))]
public sealed partial class FishBluespaceSuppressorComponent : Component
{
    /// <summary>Requested operating state; power, anchoring and station membership remain required.</summary>
    [DataField]
    public bool Enabled;

    /// <summary>Station background removed per active second.</summary>
    [DataField]
    public float SuppressionPerSecond = 0.12f;

    internal float UiElapsed = 1f;
}
