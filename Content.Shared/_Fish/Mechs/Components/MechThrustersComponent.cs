using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Mechs.Components;

    /// <summary>
    /// Маневровые ускорители: движение в невесомости за счёт энергии.
    /// </summary>
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class MechThrustersComponent : Component
    {
        [DataField, AutoNetworkedField]
        public bool Active;

        [DataField]
        public float EnergyPerSecond = 4f;

        [DataField]
        public EntProtoId ToggleAction = "ActionMechToggleThrusters";

        [DataField]
        public EntityUid? ToggleActionEntity;
    }
