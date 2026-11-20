using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемому upstream-компоненту.
namespace Content.Shared.AlertLevel;

public sealed partial class AlertLevelComponent
{
    /// <summary>
    /// Additional alert levels currently active alongside the station's primary alert level.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<AlertLevelPrototype>> ActiveAdditionalLevels = [];
}
