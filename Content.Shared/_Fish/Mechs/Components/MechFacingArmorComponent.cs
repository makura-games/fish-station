using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Направленная броня Fish: абсолютные множители урона и шансы рикошета по секторам.
/// Секторы — конусы FrontConeDegrees / RearConeDegrees, остальное — борт.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechFacingArmorComponent : Component
{
    /// <summary>Множитель входящего урона спереди.</summary>
    [DataField]
    public float FrontDamageMult = 0.85f;

    [DataField]
    public float SideDamageMult = 1f;

    [DataField]
    public float RearDamageMult = 1.4f;

    /// <summary>Абсолютный шанс полного рикошета спереди (0–1).</summary>
    [DataField, AutoNetworkedField]
    public float FrontDeflectChance = 0.12f;

    [DataField, AutoNetworkedField]
    public float SideDeflectChance = 0.06f;

    [DataField, AutoNetworkedField]
    public float RearDeflectChance = 0.02f;

    /// <summary>Половина переднего конуса в градусах (полный конус = 2×).</summary>
    [DataField]
    public float FrontConeHalfDegrees = 50f;

    /// <summary>Половина заднего конуса в градусах.</summary>
    [DataField]
    public float RearConeHalfDegrees = 50f;
}
