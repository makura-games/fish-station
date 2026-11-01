using Content.Shared._Fish.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Cargo;

/// <summary>Filters the authoritative request catalog while preserving markets and cross-department approval.</summary>
public sealed partial class FishCargoCatalogSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    /// <summary>Removes products belonging to another department from a managed console's request list.</summary>
    public void FilterProducts(Entity<CargoOrderConsoleComponent> console, List<ProtoId<CargoProductPrototype>> products)
    {
        if (!TryComp<FishCargoCatalogComponent>(console, out var settings))
            return;

        if (!_prototype.Resolve(settings.Catalog, out var catalog))
        {
            products.Clear();
            return;
        }

        products.RemoveAll(product => GetAccount(catalog, _prototype.Index(product)) != console.Comp.Account);
    }

    /// <summary>Returns the requesting department for a product, with specific exceptions taking priority.</summary>
    public static ProtoId<CargoAccountPrototype> GetAccount(FishCargoCatalogPrototype catalog, CargoProductPrototype product)
    {
        if (catalog.Products.TryGetValue(product.ID, out var account))
            return account;

        return catalog.Categories.GetValueOrDefault(product.Category, catalog.DefaultAccount);
    }
}
