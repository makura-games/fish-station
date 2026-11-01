using Content.Server._Fish.Cargo;

namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem
{
    /// <summary>Applies Fish's department routing to the native authoritative product catalog.</summary>
    [Dependency] private FishCargoCatalogSystem _fishCargoCatalog = default!;
}
