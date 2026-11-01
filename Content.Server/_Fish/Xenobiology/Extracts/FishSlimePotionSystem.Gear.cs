using Content.Shared._Sunrise.Modsuit;
using Content.Shared.Clothing.Components;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Slippery;
using Content.Shared.StepTrigger.Components;

namespace Content.Server._Fish.Xenobiology.Extracts;

public sealed partial class FishSlimePotionSystem
{
    private void OnClothingInteractUsing(Entity<ClothingComponent> clothing, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<FishSlimePotionComponent>(args.Used, out var potion))
            return;

        if (potion.Kind != FishSlimePotionKind.Fireproof)
            return;

        args.Handled = true;
        var fire = EnsureComp<FireProtectionComponent>(clothing);
        fire.Reduction = 1f;
        Dirty(clothing, fire);

        var coating = EnsureComp<FishSlimeChillCoatingComponent>(clothing);
        coating.Charges = 3;
        Dirty(clothing, coating);

        _popup.PopupEntity(Loc.GetString("fish-slime-potion-fireproof"), clothing, args.User);
        QueueDel(args.Used);
    }

    private void OnItemInteractUsing(Entity<ItemComponent> target, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<FishSlimePotionComponent>(args.Used, out var potion))
            return;

        if (potion.Kind != FishSlimePotionKind.OilSlick)
            return;

        if (HasComp<ClothingComponent>(target))
        {
            if (TryComp<ModsuitComponent>(target, out var mod) && mod.IsActivated)
            {
                args.Handled = true;
                _popup.PopupEntity(Loc.GetString("fish-slime-potion-oil-slick-mod"), args.User, args.User);
                return;
            }

            return;
        }

        args.Handled = true;
        EnsureComp<StepTriggerComponent>(target);
        EnsureComp<SlipperyComponent>(target);
        _popup.PopupEntity(Loc.GetString("fish-slime-potion-oil-slick"), target, args.User);
        QueueDel(args.Used);
    }
}
