using Robust.Shared.GameObjects;

namespace Content.Shared._Fish.PlanetWar.Drone
{
    /// <summary>
    /// Component for PlanetWar drones that stun and disorient instead of exploding.
    /// </summary>
    [RegisterComponent]
    public sealed partial class PlanetWarStunDroneComponent : Component
    {
        /// <summary>
        /// Длительность ослепления (flash) в секундах.
        /// </summary>
        [DataField]
        public float FlashDuration = 8f;

        [DataField]
        public float FlashRange = 2.5f;

        [DataField]
        public float ElectrocutionRange = 1.5f;
    }
}
