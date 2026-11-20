// Fish-start - перенесено в Content.Shared.Guardian.Components.GuardianComponent после апстрима
/*
using Robust.Shared.Audio;

namespace Content.Server.Guardian
{
    /// <summary>
    /// Given to guardians to monitor their link with the host
    /// </summary>
    [RegisterComponent]
    public sealed partial class GuardianComponent : Component
    {
        /// <summary>
        /// The guardian host entity
        /// </summary>
        [DataField]
        public EntityUid? Host;

        /// <summary>
        /// Percentage of damage reflected from the guardian to the host
        /// </summary>
        [DataField]
        public float DamageShare { get; set; } = 0.65f;

        /// <summary>
        /// Maximum distance the guardian can travel before it's forced to recall, use YAML to set
        /// </summary>
        [DataField]
        public float DistanceAllowed { get; set; } = 5f;

        /// <summary>
        /// If the guardian is currently manifested
        /// </summary>
        [DataField]
        public bool GuardianLoose;

        /// <summary>
        /// Sound played when a mob starts hosting the guardian.
        /// </summary>
        [DataField]
        public SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Effects/guardian_inject.ogg");

        /// <summary>
        /// Sound played when the guardian enters critical state.
        /// </summary>
        [DataField]
        public SoundSpecifier CriticalSound = new SoundPathSpecifier("/Audio/Effects/guardian_warn.ogg");

        /// <summary>
        /// Sound played when the guardian dies.
        /// </summary>
        [DataField]
        public SoundSpecifier DeathSound = new SoundPathSpecifier("/Audio/Voice/Human/malescream_guardian.ogg", AudioParams.Default.WithVariation(0.2f));

        // Fish-start
        /// <summary>
        /// If true, the guardian is too far from the host and cannot interact.
        /// </summary>
        [DataField]
        public bool DisableInteraction;
        // Fish-end
    }
}
*/
// Fish-end

