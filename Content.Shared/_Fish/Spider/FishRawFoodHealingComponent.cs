using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Spider;

/// <summary>
/// Добавляет лечебный реагент в кровоток при съедании сырой пищи или органов.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishRawFoodHealingComponent : Component
{
    /// <summary>
    /// Реагент, добавляемый в организм за каждое съеденное сырое мясо или орган.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> HealReagent = "Ichor";

    /// <summary>
    /// Количество добавляемого реагента.
    /// </summary>
    [DataField]
    public FixedPoint2 HealAmount = FixedPoint2.New(3);

    /// <summary>
    /// Дополнительная сытость, начисляемая за каждое полностью съеденное сырое мясо или орган.
    /// </summary>
    [DataField]
    public float HungerBonus = 20f;
}
