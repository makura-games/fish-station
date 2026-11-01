using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Mech;

/// <summary>
/// Делает мех уязвимым к электромагнитным импульсам
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MechVulnerableToEMPComponent : Component
{
    [ViewVariables]
    public TimeSpan NextPulseTime;

    /// <summary> fish-edit start
    /// [DataField]
    /// public TimeSpan CooldownTime = TimeSpan.FromSeconds(6);
    /// </summary> fish-edit end

    [DataField]
    public DamageSpecifier EmpDamage = new()
    {
        DamageDict = new()
        {
            { "Shock", 25f },
        }
    };

    [DataField]
    public EntProtoId EffectEMP = "EffectMechSparks";
}
