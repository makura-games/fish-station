using Content.Shared._Fish.Netrunner;

namespace Content.Server._Fish.Netrunner;

/// <summary>Bounded device audit with no attacker identity.</summary>
[RegisterComponent]
public sealed partial class NetrunnerAuditComponent : Component
{
    public readonly List<NetrunnerLogEntry> Entries = new();
    /// <summary>Maximum entry age for an airlock's volatile diagnostic buffer.</summary>
    public TimeSpan? Retention;
}
