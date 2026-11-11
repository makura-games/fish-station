using Content.Shared.Body;
using Content.Shared.Starlight.Medical.Surgery;
using Robust.Client.GameObjects;
using Robust.Client.Player;

namespace Content.Client._Starlight.Medical.Surgery;

public sealed partial class SurgerySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private EntityQuery<UserInterfaceUserComponent> _uiUserQuery = default!;

    private void InitializeFishUiInvalidation()
    {
        SubscribeLocalEvent<AppearanceComponent, AppearanceChangeEvent>(OnFishAppearanceChanged);
        SubscribeLocalEvent<TransformComponent, MoveEvent>(OnFishMoved);
        SubscribeLocalEvent<SurgeryComponent, ComponentShutdown>(OnFishSurgeryShutdown);
        SubscribeLocalEvent<SurgeryTargetComponent, OrganInsertedIntoEvent>(OnFishOrganInserted);
        SubscribeLocalEvent<SurgeryTargetComponent, OrganRemovedFromEvent>(OnFishOrganRemoved);
    }

    private void OnFishAppearanceChanged(Entity<AppearanceComponent> ent, ref AppearanceChangeEvent args)
    {
        if (_ui.TryGetOpenUi<SurgeryBui>(ent.Owner, SurgeryUIKey.Key, out var bui))
            bui.RefreshFishAppearance();
    }

    private void OnFishOrganInserted(Entity<SurgeryTargetComponent> ent, ref OrganInsertedIntoEvent args)
    {
        RefreshFishAnatomy(ent);
    }

    private void OnFishOrganRemoved(Entity<SurgeryTargetComponent> ent, ref OrganRemovedFromEvent args)
    {
        RefreshFishAnatomy(ent);
    }

    private void OnFishMoved(Entity<TransformComponent> ent, ref MoveEvent args)
    {
        if (ent.Owner == _player.LocalEntity)
        {
            RefreshOpenFishUis();
            return;
        }

        if (HasComp<SurgeryTargetComponent>(ent.Owner) &&
            _ui.TryGetOpenUi<SurgeryBui>(ent.Owner, SurgeryUIKey.Key, out var bui))
        {
            bui.QueueFishUiRefresh();
        }
    }

    private void OnFishSurgeryShutdown(Entity<SurgeryComponent> ent, ref ComponentShutdown args)
    {
        if (_player.LocalEntity is not { } user || !_uiUserQuery.TryComp(user, out var interfaces))
            return;

        foreach (var patient in interfaces.OpenInterfaces.Keys)
        {
            if (_ui.TryGetOpenUi<SurgeryBui>(patient, SurgeryUIKey.Key, out var bui))
                bui.InvalidateFishSurgery(ent.Owner);
        }
    }

    private void RefreshOpenFishUis()
    {
        if (_player.LocalEntity is not { } user || !_uiUserQuery.TryComp(user, out var interfaces))
            return;

        // Движок уже хранит открытые BUI игрока; отдельный реестр и обход всех пациентов не нужны.
        foreach (var patient in interfaces.OpenInterfaces.Keys)
        {
            if (_ui.TryGetOpenUi<SurgeryBui>(patient, SurgeryUIKey.Key, out var bui))
                bui.QueueFishUiRefresh();
        }
    }

    private void RefreshFishAnatomy(EntityUid patient)
    {
        if (!_ui.TryGetOpenUi<SurgeryBui>(patient, SurgeryUIKey.Key, out var bui))
            return;

        // Состояние контейнера и снимок BUI могут прийти в разном порядке.
        bui.RefreshFishAppearance();
        bui.QueueFishUiRefresh();
    }
}
