using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Внутренние повреждения шасси: флаги, порог и шанс при уроне.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechInternalDamageComponent : Component
{
    [DataField, AutoNetworkedField]
    public MechInternalDamageFlags Damage = MechInternalDamageFlags.None;

    /// <summary>
    /// Доля Integrity/MaxIntegrity, выше которой возможен ролл внутреннего урона.
    /// </summary>
    [DataField]
    public float IntegrityThreshold = 0.55f;

    /// <summary>
    /// Шанс получить внутреннее повреждение при превышении порога (0–1).
    /// </summary>
    [DataField]
    public float DamageChance = 0.28f;

    /// <summary>
    /// Энергия/сек при PowerSpike.
    /// </summary>
    [DataField]
    public float PowerSpikeDrainPerSecond = 6f;

    /// <summary>
    /// Урон Heat/сек при CabinFire.
    /// </summary>
    [DataField]
    public float CabinFireDamagePerSecond = 1.5f;
}
