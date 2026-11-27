using Content.Shared._Sunrise.Laws;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Fish.SecurityCrimeReport;

/// <summary>
/// Grants a hotbar InstantAction while the security gas mask is worn.
/// Opens a SimpleRadialMenu of frequently used corporate-law articles.
/// FIsh edit
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class SecurityCrimeReportComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionSecurityCrimeReport";

    [AutoNetworkedField, ViewVariables]
    public EntityUid? ActionEntity;

    /// <summary>
    /// Curated quick-report articles from FishCorporateLaw / Fish Station Wiki Space Law.
    /// </summary>
    [DataField]
    public List<ProtoId<CorporateLawPrototype>> Articles = new()
    {
        "LawFish101",
        "LawFish201",
        "LawFish301",
        "LawFish312",
        "LawFish302",
        "LawFish303",
        "LawFish304",
        "LawFish401",
        "LawFish502",
    };

    /// <summary>
    /// Cooldown после успешной отправки доклада (UI Action + UseDelay на носителе).
    /// </summary>
    [DataField]
    public TimeSpan ReportCooldown = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan MalfunctionDuration = TimeSpan.FromSeconds(60);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan? MalfunctionUntil;
}
