using Content.Shared._Fish.Xenobiology.Extracts;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;

namespace Content.Server._Fish.Xenobiology.Extracts;

/// <summary>
/// Списание зарядов chill-покрытия при огненном уроне.
/// </summary>
public sealed partial class FishSlimeChillCoatingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishSlimeChillCoatingComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<FishSlimeChillCoatingComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta is not { } delta || !delta.DamageDict.TryGetValue("Heat", out var heat) || heat <= FixedPoint2.Zero)
            return;

        if (!HasComp<ClothingComponent>(ent))
            return;

        ent.Comp.Charges--;
        if (ent.Comp.Charges <= 0)
        {
            RemComp<FireProtectionComponent>(ent);
            RemComp<FishSlimeChillCoatingComponent>(ent);
            return;
        }

        Dirty(ent);
    }
}
