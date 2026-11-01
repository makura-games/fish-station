using Content.Shared._Fish.Cargo;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Cargo;

/// <summary>Restricts a cargo console's request catalog to its account's departmental supplies.</summary>
[RegisterComponent, Access(typeof(FishCargoCatalogSystem))]
public sealed partial class FishCargoCatalogComponent : Component
{
    /// <summary>Catalog defining which departmental account may request each product.</summary>
    [DataField]
    public ProtoId<FishCargoCatalogPrototype> Catalog = "FishStationCargoCatalog";
}
