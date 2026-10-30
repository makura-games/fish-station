using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Ocular Warden — турель, жжёт неслуг в радиусе.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishOcularWardenComponent : Component
{
    [DataField]
    public float Range = FishClockworkCultConstants.OcularWardenRange;

    [DataField]
    public float ExclusionRange = FishClockworkCultConstants.OcularWardenExclusionRange;

    [DataField]
    public float DamagePerTick = 5f;

    [DataField]
    public float TickInterval = 1f;

    [ViewVariables]
    public float Accumulator;

    [DataField]
    public SoundSpecifier AttackSound = new SoundPathSpecifier("/Audio/_Fish/ClockworkCult/machines/clockcult/ocularwarden-target.ogg");
}
