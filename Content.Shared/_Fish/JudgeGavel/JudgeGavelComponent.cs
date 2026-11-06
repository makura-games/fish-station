using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.JudgeGavel;

/// <summary>
///     Component for the Admin Judge Gavel.
///     When activated, starts a DoAfter that teleports sentient creatures in a radius to the Centcomm courtroom.
///     Fish-edit
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class JudgeGavelComponent : Component
{
    [DataField]
    public float Range = 10f;

    [DataField]
    public float Duration = 900f; // Seconds of pacifism

    [DataField]
    public string CourtroomBeaconId = "station-beacon-courtroom";

    [DataField]
    public LocId Chant = "judge-gavel-chant";

    [DataField]
    public float DoAfterTime = 3f;

    /// <summary>
    ///     Tracks the current active DoAfter to prevent multiple concurrent swings.
    /// </summary>
    public DoAfterId? ActiveDoAfter;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId PacifiedStatusEffect = "StatusEffectPacified";
}
