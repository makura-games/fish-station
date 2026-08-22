using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.BattleShuttles.Components;

/// <summary>
/// Data-driven модуль шаттла поверх <c>MechEquipment</c>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BattleShuttleModuleComponent : Component
{
    /// <summary>
    /// Уникальный слот (weapon, cargo, lock...). Пусто — слот не уникален.
    /// </summary>
    [DataField]
    public string Slot = string.Empty;

    [DataField]
    public int OccupantMod;

    [DataField]
    public float MassModifier = 1f;

    [DataField]
    public float WalkSpeedModifier = 1f;

    [DataField]
    public float SprintSpeedModifier = 1f;

    /// <summary>
    /// Стоимость для каталогов/исследований. На геймплей не влияет.
    /// </summary>
    [DataField]
    public int Cost;

    /// <summary>
    /// Пусто — совместим с любым шаттлом. Иначе нужно пересечение с ClassTags.
    /// </summary>
    [DataField]
    public List<ProtoId<TagPrototype>> CompatibleShuttleTags = [];
}
