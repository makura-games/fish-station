using Content.Shared.Damage;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Ipc;

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
    public int FuelCost = 5;

    [DataField]
    public float DoAfterDelay = 3f;

    [DataField]
    public float SelfHealPenalty = 3f;

    [DataField]
    public bool AllowSelfHeal = true;

    [DataField(required: true)]
    public List<string> DamageContainers = default!;

    [DataField]
    public bool RequireWelderFuel = true;

    [DataField]
    public int StackCost = 1;
}
