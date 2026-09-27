// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/space-sunrise/space-station-14/blob/master/CLA.txt
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Content.Shared._Sunrise.Disease;
using Content.Shared.Ghost;
namespace Content.Client._Sunrise.Disease;
public sealed partial class SickSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SickComponent, GetStatusIconsEvent>(OnGetStatusIconsGlobal);
        SubscribeNetworkEvent<UpdateInfectionsEvent>(OnUpdateInfect);
    }

    private void OnUpdateInfect(UpdateInfectionsEvent args)
    {
        EnsureComp<SickComponent>(GetEntity(args.Uid)).Inited = true;
    }

    private void OnGetStatusIconsGlobal(EntityUid uid, SickComponent component, ref GetStatusIconsEvent args)
    {
        if (_playerManager.LocalEntity == null)
            return;

        if (!HasComp<DiseaseRoleComponent>(_playerManager.LocalEntity.Value))
            return;

        if (_mobState.IsDead(uid) || HasComp<ActiveNPCComponent>(uid))
            return;

        if (!component.Inited)
            return;

        var iconId = "SmartDiseaseIcon";
        args.StatusIcons.Add(_prototype.Index<SickIconPrototype>(iconId));
    }
}

