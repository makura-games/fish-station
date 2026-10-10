using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Mech.Components;

/// <summary>
/// Настройки меха для установки роботизированного мозга, pAI или MMI.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MechBrainComponent : Component
{
    /// <summary>
    /// Разрешена ли установка сущностей с BorgBrainComponent в кабину меха.
    /// </summary>
    [DataField]
    public bool CanInsertBrain;
}
