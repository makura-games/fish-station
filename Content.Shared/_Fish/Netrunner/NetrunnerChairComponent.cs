namespace Content.Shared._Fish.Netrunner;

/// <summary>A powered, single-occupant camera interface for an implanted netrunner.</summary>
[RegisterComponent]
public sealed partial class NetrunnerChairComponent : Component
{
    [DataField] public TimeSpan ConnectTime = TimeSpan.FromSeconds(5);
    [DataField] public TimeSpan ChargeInterval = TimeSpan.FromSeconds(20);
    [DataField] public float ConnectionCost = 1;
    [DataField] public float ConnectionTrace = 3;
    [DataField] public float UpkeepCost = 1;
    [DataField] public float UpkeepTrace = 1;
    /// <summary>Idle RAM regeneration while the cooling unit is powered.</summary>
    [DataField] public float RamPerSecond = 0.4f;
    /// <summary>Trace decay while connected to the powered chair.</summary>
    [DataField] public float TracePerSecond = 0.3f;
    /// <summary>Time needed to compile one credit without active or noisy programs.</summary>
    [DataField] public TimeSpan LinkInterval = TimeSpan.FromSeconds(60);
    /// <summary>Maximum passive credits from this implant during a round.</summary>
    [DataField] public int MaxPassiveLinks = 20;
    /// <summary>Highest trace at which background compilation is allowed.</summary>
    [DataField] public float CompilationTraceLimit = 30;
}
