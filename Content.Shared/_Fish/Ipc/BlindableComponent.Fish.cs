using Content.Shared.Eye.Blinding.Systems;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Eye.Blinding.Components;

public sealed partial class BlindableComponent
{
    /// <summary>
    /// Накопитель дробного лечения повреждений глаз (например, 0.5 за применение нанопасты).
    /// </summary>
    [ViewVariables, AutoNetworkedField, Access(typeof(BlindableSystem))]
    public float EyeHealBuffer;
}
