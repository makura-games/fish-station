using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Ipc;

[RegisterComponent]
public sealed partial class FishWeldingHealableComponent : Component;

/// <summary>Инструмент ремонта КПБ (сварка) — Paradise welder.</summary>
[RegisterComponent]
public sealed partial class FishWeldingHealingComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage = default!;

    [DataField]
    public ProtoId<ToolQualityPrototype> QualityNeeded = "Welding";

    [DataField]
    public FixedPoint2 FuelCost = 0.5;

    [DataField]
    public float DoAfterDelay = 1f;

    /// <summary>Самосварка дольше чужого ремонта во столько раз.</summary>
    [DataField]
    public float SelfHealPenalty = 2f;

    [DataField]
    public bool AllowSelfHeal = true;

    [DataField(required: true)]
    public List<string> DamageContainers = default!;

    [DataField]
    public bool RequireWelderFuel = true;

    [DataField]
    public int StackCost = 1;
}
