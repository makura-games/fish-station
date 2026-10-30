using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Wraith Spectacles — true sight с постепенным уроном носителю.
/// </summary>
// Fish-edit: без AutoGenerateComponentState — накопленный урон только серверный.
[RegisterComponent]
public sealed partial class FishWraithSpectaclesComponent : Component
{
    [DataField]
    public float EyeDamagePerSecond = 0.5f;

    [DataField]
    public float HealPerSecondUnequipped = 0.25f;

    /// <summary>
    /// Накопленный «глазной» урон (лечится при снятии). Только сервер.
    /// </summary>
    public float AccumulatedEyeDamage;

    [ViewVariables]
    public EntityUid? Wearer;
}
