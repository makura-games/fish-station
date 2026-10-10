using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Partial-класс расширяет ванильный MechComponent из папки Fish.
namespace Content.Shared.Mech.Components;
#pragma warning restore IDE0130

public sealed partial class MechComponent
{
    /// <summary>
    /// Действие сканера массы, которое выдаётся пилоту при посадке в мех.
    /// </summary>
    [DataField]
    public EntProtoId? MechMassScannerAction;

    /// <summary>
    /// Сущность действия сканера массы, выданного текущему пилоту.
    /// </summary>
    public EntityUid? MechMassScannerActionEntity;
}
