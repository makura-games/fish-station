using Content.Shared._Fish.Clothing;
using Content.Shared.Armor;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion;
using Content.Shared.Foldable;
using Content.Shared.Inventory;

namespace Content.Server._Fish.Clothing;

/// <summary>
/// Applies closed-visor protection and manages the wearer's overlay state.
/// </summary>
public sealed partial class VisorSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VisorComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<VisorComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<VisorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VisorComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VisorComponent, VisorToggledEvent>(OnVisorToggled);
        SubscribeLocalEvent<VisorComponent, InventoryRelayedEvent<CoefficientQueryEvent>>(OnCoefficientQuery);
        SubscribeLocalEvent<VisorComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
        SubscribeLocalEvent<VisorComponent, InventoryRelayedEvent<GetExplosionResistanceEvent>>(OnExplosionResistance);
    }

    private void OnEquipped(Entity<VisorComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (ent.Comp.CurrentWearer is { } previousWearer && previousWearer != args.Wearer)
            DisableOverlay(ent, previousWearer);

        ent.Comp.CurrentWearer = args.Wearer;
        RefreshOverlay(ent, args.Wearer);
    }

    private void OnUnequipped(Entity<VisorComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        DisableOverlay(ent, args.Wearer);

        if (ent.Comp.CurrentWearer == args.Wearer)
            ent.Comp.CurrentWearer = null;
    }

    private void OnStartup(Entity<VisorComponent> ent, ref ComponentStartup args)
    {
        if (!TryGetWearer(ent, out var wearer))
            return;

        ent.Comp.CurrentWearer = wearer;
        RefreshOverlay(ent, wearer);
    }

    private void OnShutdown(Entity<VisorComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.CurrentWearer is not { } wearer && !TryGetWearer(ent, out wearer))
            return;

        DisableOverlay(ent, wearer);
        ent.Comp.CurrentWearer = null;
    }

    private void OnVisorToggled(Entity<VisorComponent> ent, ref VisorToggledEvent args)
    {
        if (ent.Comp.CurrentWearer is not { } wearer && !TryGetWearer(ent, out wearer))
            return;

        ent.Comp.CurrentWearer = wearer;
        RefreshOverlay(ent, wearer);
    }

    private void OnCoefficientQuery(Entity<VisorComponent> ent,
        ref InventoryRelayedEvent<CoefficientQueryEvent> args)
    {
        if (!IsClosed(ent))
            return;

        foreach (var (damageType, coefficient) in ent.Comp.ClosedDamageModifiers.Coefficients)
        {
            args.Args.DamageModifiers.Coefficients[damageType] =
                args.Args.DamageModifiers.Coefficients.TryGetValue(damageType, out var current)
                    ? current * coefficient
                    : coefficient;
        }
    }

    private void OnDamageModify(Entity<VisorComponent> ent,
        ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (!IsClosed(ent))
            return;

        args.Args.Damage = DamageSpecifier.ApplyModifierSet(
            args.Args.Damage,
            ent.Comp.ClosedDamageModifiers,
            args.Args.ArmorPenetration,
            args.Args.CanHeal);
    }

    private void OnExplosionResistance(Entity<VisorComponent> ent,
        ref InventoryRelayedEvent<GetExplosionResistanceEvent> args)
    {
        if (IsClosed(ent))
            args.Args.DamageCoefficient *= ent.Comp.ClosedExplosionCoefficient;
    }

    private void RefreshOverlay(Entity<VisorComponent> ent, EntityUid wearer)
    {
        if (!IsClosed(ent))
        {
            DisableOverlay(ent, wearer);
            return;
        }

        var overlay = EnsureComp<VisorOverlayComponent>(wearer);
        overlay.Source = ent;
        overlay.OpeningWidth = ent.Comp.OpeningWidth;
        overlay.OpeningHeight = ent.Comp.OpeningHeight;
        overlay.EdgeSoftness = ent.Comp.EdgeSoftness;
        overlay.CornerRadius = ent.Comp.CornerRadius;
        overlay.Darkness = ent.Comp.Darkness;
        overlay.FadeDuration = ent.Comp.FadeDuration;
        Dirty(wearer, overlay);
    }

    private void DisableOverlay(Entity<VisorComponent> ent, EntityUid wearer)
    {
        if (!TryComp<VisorOverlayComponent>(wearer, out var overlay) || overlay.Source != ent.Owner)
            return;

        RemComp<VisorOverlayComponent>(wearer);
    }

    private bool IsClosed(EntityUid uid)
    {
        return TryComp<FoldableComponent>(uid, out var foldable) && !foldable.IsFolded;
    }

    private bool TryGetWearer(EntityUid uid, out EntityUid wearer)
    {
        wearer = EntityUid.Invalid;

        if (!_inventory.TryGetContainingSlot(uid, out _))
            return false;

        wearer = Transform(uid).ParentUid;
        return wearer.IsValid();
    }
}
