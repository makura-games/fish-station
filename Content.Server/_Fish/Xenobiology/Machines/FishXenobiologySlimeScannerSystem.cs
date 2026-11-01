using Content.Shared._Fish.Xenobiology.Machines;
using Content.Shared._Fish.Xenobiology.Slimes;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Server._Fish.Xenobiology.Machines;

public sealed partial class FishXenobiologySlimeScannerSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishXenobiologySlimeScannerComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<FishXenobiologySlimeScannerComponent> scanner, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!TryComp<FishSlimeComponent>(target, out var slime))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("fish-slime-examine",
            ("nutrition", (int) slime.Nutrition),
            ("max", (int) slime.MaxNutrition),
            ("grown", slime.AmountGrown),
            ("need", FishSlimeMutation.EvolutionThreshold),
            ("mutation", slime.MutationChance),
            ("cores", slime.Cores),
            ("power", slime.PowerLevel),
            ("adult", slime.IsAdult),
            ("rabid", slime.Rabid),
            ("docile", slime.Docile),
            ("trained", slime.Trained)), target, args.User);
    }
}
