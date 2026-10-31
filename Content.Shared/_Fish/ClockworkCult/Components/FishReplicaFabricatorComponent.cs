using Robust.Shared.GameStates;

namespace Content.Shared._Fish.ClockworkCult.Components;

/// <summary>
/// Replica Fabricator — конвертация объектов и производство латуни.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishReplicaFabricatorComponent : Component
{
    [DataField, AutoNetworkedField]
    public float StoredPower;

    [DataField]
    public float SheetToPower = FishClockworkCultConstants.PowerMetal;

    [DataField]
    public float FloorCost = FishClockworkCultConstants.PowerFloor;

    [DataField]
    public float WallCost = FishClockworkCultConstants.PowerWallTotal;
}
