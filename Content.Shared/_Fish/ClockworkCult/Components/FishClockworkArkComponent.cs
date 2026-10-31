using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Ковчег Clockwork Justiciar на Reebe.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishClockworkArkComponent : Component
{
    [DataField, AutoNetworkedField]
    public FishClockworkArkPhase Phase = FishClockworkArkPhase.Prep;

    /// <summary>
    /// Секунды до активации в фазе Prep.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SecondsUntilActivation = -1f;

    [DataField, AutoNetworkedField]
    public float InitialActivationDelay = -1f;

    [DataField, AutoNetworkedField]
    public float ProgressSeconds;

    [DataField, AutoNetworkedField]
    public float GracePeriodRemaining = FishClockworkCultConstants.ArkGracePeriodSeconds;

    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public bool PurposeFulfilled;

    [DataField]
    public int RecallsRemaining = 1;

    [ViewVariables]
    public bool Recalling;

    [ViewVariables]
    public TimeSpan? LastScreamTime;

    [DataField]
    public EntProtoId RatvarSpawnPrototype = "MobRatvarSpawn";

    [DataField]
    public EntProtoId RiftPrototype = "FishCityOfCogsRift";

    [DataField]
    public SoundSpecifier DamageSound = new SoundPathSpecifier("/Audio/_Fish/ClockworkCult/machines/clockcult/ark_damage.ogg");

    [DataField]
    public SoundSpecifier ScreamSound = new SoundPathSpecifier("/Audio/_Fish/ClockworkCult/machines/clockcult/ark_scream.ogg");

    [DataField]
    public SoundSpecifier ActivationSound = new SoundPathSpecifier("/Audio/_Fish/ClockworkCult/magic/clockwork/ark_activation.ogg");

    [DataField]
    public SoundSpecifier RecallSound = new SoundPathSpecifier("/Audio/_Fish/ClockworkCult/machines/clockcult/ark_recall.ogg");
}
