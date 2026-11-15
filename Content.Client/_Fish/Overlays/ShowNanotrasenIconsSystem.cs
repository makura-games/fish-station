using Content.Shared._Fish.Overlays;
using Content.Shared.ERTMembers;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;

namespace Content.Client.Overlays;

public sealed partial class ShowNanotrasenIconsSystem : EquipmentHudSystem<ShowNanotrasenIconsComponent>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ERTMemberComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    private void OnGetStatusIconsEvent(EntityUid uid, ERTMemberComponent component, ref GetStatusIconsEvent ev)
    {
        if (!IsActive)
            return;

        if (ProtoMan.TryIndex<FactionIconPrototype>(component.NanotrasenStatusIcon, out var iconPrototype))
            ev.StatusIcons.Add(iconPrototype);
    }
}
