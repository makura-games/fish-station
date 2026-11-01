using Content.Shared._Fish.Xenobiology.Organs;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Server._Fish.Xenobiology.Organs;

public sealed partial class FishXenoRegenMeshSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishXenoOrganComponent, InteractUsingEvent>(OnOrganInteractUsing);
    }

    private void OnOrganInteractUsing(Entity<FishXenoOrganComponent> organ, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<FishXenoRegenMeshComponent>(args.Used))
            return;

        if (!organ.Comp.Identified)
        {
            _popup.PopupEntity(Loc.GetString("fish-xeno-regen-mesh-unidentified"), args.User, args.User);
            return;
        }

        args.Handled = true;

        if (organ.Comp.Quality == FishXenoOrganQuality.Pristine)
        {
            _popup.PopupEntity(Loc.GetString("fish-xeno-regen-mesh-already"), args.User, args.User);
            return;
        }

        organ.Comp.Quality = FishXenoOrganQuality.Pristine;
        Dirty(organ, organ.Comp);
        _popup.PopupEntity(Loc.GetString("fish-xeno-regen-mesh-success"), organ, args.User);
        QueueDel(args.Used);
    }
}
