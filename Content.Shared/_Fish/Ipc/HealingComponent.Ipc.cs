#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Medical.Healing;

public sealed partial class HealingComponent
{
    /// <summary>
    /// Количество повреждений глаз (<c>BlindableComponent.EyeDamage</c>), снимаемое за одно применение.
    /// </summary>
    [DataField]
    public float EyeDamageHeal;
}
