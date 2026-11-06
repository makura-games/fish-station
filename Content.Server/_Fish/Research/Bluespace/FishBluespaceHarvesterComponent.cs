using Content.Shared._Fish.Research.Bluespace;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Research.Bluespace;

/// <summary>Configures a high-voltage harvester and stores its bounded energy accounting.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class FishBluespaceHarvesterComponent : Component
{
    /// <summary>Whether the operator has enabled the harvester.</summary>
    [DataField]
    public bool Enabled;

    /// <summary>Requested power in normal mode, in watts.</summary>
    [DataField]
    public float NormalPowerDraw = 125_000f;

    /// <summary>Requested power in overdrive mode, in watts.</summary>
    [DataField]
    public float OverdrivePowerDraw = 250_000f;

    /// <summary>Real received energy required for one reward, in joules.</summary>
    [DataField]
    public float EnergyPerReward = 7_500_000f;

    /// <summary>Station background added per megajoule of actual consumption.</summary>
    [DataField]
    public float BackgroundPerMegajoule = 1.2f;

    /// <summary>Additional background multiplier while using overdrive.</summary>
    [DataField]
    public float OverdriveBackgroundMultiplier = 2f;

    /// <summary>Local critical threshold, in addition to the shared station interlock.</summary>
    [DataField]
    public float CriticalLevel = 95f;

    /// <summary>Background must fall below this threshold before a local scram can be reset.</summary>
    [DataField]
    public float RestartLevel = 80f;

    /// <summary>Station-wide incursion cooldown, never shorter than forty seconds.</summary>
    [DataField]
    public TimeSpan IncidentCooldown = TimeSpan.FromSeconds(40);

    /// <summary>Maximum living incursion creatures per station, bounded to six.</summary>
    [DataField]
    public int MaxActiveIncursions = 6;

    /// <summary>Creatures released in a wave above the station incursion threshold.</summary>
    [DataField]
    public int IncursionWaveSize = 2;

    /// <summary>Creatures released in a wave above the station danger threshold.</summary>
    [DataField]
    public int DangerousWaveSize = 3;

    /// <summary>Station-wide anomaly cooldown, never shorter than two minutes.</summary>
    [DataField]
    public TimeSpan AnomalyCooldown = TimeSpan.FromSeconds(120);

    /// <summary>Maximum active harvester anomalies per station, bounded to two.</summary>
    [DataField]
    public int MaxActiveAnomalies = 2;

    /// <summary>Native anomaly created above the station danger threshold.</summary>
    [DataField]
    public EntProtoId AnomalyPrototype = "AnomalyBluespace";

    /// <summary>Lifetime of an incursion creature, bounded to three minutes.</summary>
    [DataField]
    public TimeSpan IncursionLifetime = TimeSpan.FromSeconds(180);

    /// <summary>Temporary visual effect used for a local incursion.</summary>
    [DataField]
    public EntProtoId RiftPrototype = "FishBluespaceRift";

    /// <summary>Native hostile creature released by a local incursion.</summary>
    [DataField]
    public EntProtoId HostilePrototype = "MobCarp";

    /// <summary>Selected power consumption mode.</summary>
    [DataField]
    public FishBluespacePowerMode PowerMode;

    /// <summary>Selected reward table category.</summary>
    [DataField]
    public FishBluespaceLootCategory LootCategory;

    /// <summary>Accumulated actual energy, capped at a single reward.</summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public double StoredEnergy;

    /// <summary>Whether the local critical interlock is engaged.</summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Critical;

    /// <summary>Whether a completed reward is waiting for a safe output tile.</summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool OutputBlocked;

    /// <summary>Prevents nested spawn callbacks from claiming the same energy.</summary>
    public bool Producing;

    /// <summary>Requires a fresh power-network update after any requested-load change.</summary>
    public bool SkipPowerSample;

    /// <summary>Next bounded output and incursion check.</summary>
    [AutoPausedField]
    public TimeSpan NextOutputCheck;

    /// <summary>Next interface refresh.</summary>
    [AutoPausedField]
    public TimeSpan NextUiUpdate;
}
