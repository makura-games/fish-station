using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Mech;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Mechs;

/// <summary>
/// BUI бортового медмодуля: пациент и список реагентов для дозированной инъекции.
/// </summary>
[Serializable, NetSerializable]
public sealed class MechMedicalSleeperUiState : BoundUserInterfaceState
{
    public List<NetEntity> Contents = new();
    public int MaxContents;
    public List<MechSleeperReagentEntry> Reagents = new();
    public FixedPoint2 InjectAmount;
}

[Serializable, NetSerializable]
public sealed class MechSleeperReagentEntry
{
    public string ReagentId = string.Empty;
    public string DisplayName = string.Empty;
    public FixedPoint2 Quantity;
}

[Serializable, NetSerializable]
public sealed class MechMedicalSleeperInjectMessage : MechEquipmentUiMessage
{
    public string ReagentId = string.Empty;

    public MechMedicalSleeperInjectMessage(NetEntity equipment, string reagentId)
    {
        Equipment = equipment;
        ReagentId = reagentId;
    }
}
