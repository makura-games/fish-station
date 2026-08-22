using Content.Shared._Fish.Mechs;
using Content.Shared.Mech;
using Content.Client.UserInterface.Fragments;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._Fish.Mechs;

public sealed partial class MechMedicalSleeperUi : UIFragment
{
    private MechMedicalSleeperUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        if (fragmentOwner == null)
            return;

        _fragment = new MechMedicalSleeperUiFragment();

        _fragment.OnEjectAction += patient =>
        {
            var entManager = IoCManager.Resolve<IEntityManager>();
            userInterface.SendMessage(new MechGrabberEjectMessage(
                entManager.GetNetEntity(fragmentOwner.Value),
                entManager.GetNetEntity(patient)));
        };

        _fragment.OnInjectAction += reagentId =>
        {
            var entManager = IoCManager.Resolve<IEntityManager>();
            userInterface.SendMessage(new MechMedicalSleeperInjectMessage(
                entManager.GetNetEntity(fragmentOwner.Value),
                reagentId));
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not MechMedicalSleeperUiState sleeperState)
            return;

        _fragment?.UpdateContents(sleeperState);
    }
}
