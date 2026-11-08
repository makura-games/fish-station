using Content.Shared._Fish.CameraHearing;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.SurveillanceCamera.Components;

namespace Content.Server._Fish.CameraHearing;

public sealed class FishCameraUpgradeSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishCameraUpgradeComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<FishCameraUpgradeComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<SurveillanceCameraComponent>(target))
            return;

        args.Handled = true;

        if (HasComp<FishCameraHearingComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.AlreadyUpgradedPopup), target, args.User);
            return;
        }

        AddComp<FishCameraHearingComponent>(target);
        _popup.PopupEntity(Loc.GetString(ent.Comp.UpgradedPopup), target, args.User);
        QueueDel(ent);
    }
}
