using Content.Shared._Fish.Xenobiology.Extracts;
using Content.Shared.Interaction;

namespace Content.Server._Fish.Xenobiology.Extracts;

public sealed partial class FishSlimePotionSystem
{
    [Dependency] private FishSlimeExtractSystem _extracts = default!;

    private void OnExtractInteractUsing(Entity<FishSlimeExtractComponent> extract, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<FishSlimePotionComponent>(args.Used, out var potion))
            return;

        if (potion.Kind != FishSlimePotionKind.Enhancer)
            return;

        args.Handled = true;
        if (!_extracts.TryAddUse(extract, args.User))
            return;

        _popup.PopupEntity(Loc.GetString("fish-slime-potion-enhancer"), extract, args.User);
        QueueDel(args.Used);
    }
}
