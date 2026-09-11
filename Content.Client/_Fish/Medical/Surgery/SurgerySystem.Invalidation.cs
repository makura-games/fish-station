using Content.Shared.Starlight.Medical.Surgery;
using Robust.Client.GameObjects;
using Robust.Client.Player;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемой upstream-системе.
namespace Content.Client._Starlight.Medical.Surgery;

public sealed partial class SurgerySystem
{
    [Dependency] private readonly IPlayerManager _fishPlayer = default!;

    private void InitializeFishUiInvalidation()
    {
        SubscribeLocalEvent<AppearanceComponent, AppearanceChangeEvent>(OnFishAppearanceChanged);
        SubscribeLocalEvent<TransformComponent, MoveEvent>(OnFishMoved);
        SubscribeLocalEvent<SurgeryComponent, ComponentShutdown>(OnFishSurgeryShutdown);
    }

    private void OnFishAppearanceChanged(Entity<AppearanceComponent> ent, ref AppearanceChangeEvent args)
    {
        if (_ui.TryGetOpenUi<SurgeryBui>(ent.Owner, SurgeryUIKey.Key, out var bui))
            bui.RefreshFishAppearance();
    }

    private void OnFishMoved(Entity<TransformComponent> ent, ref MoveEvent args)
    {
        if (ent.Owner == _fishPlayer.LocalEntity)
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
        var query = EntityQueryEnumerator<SurgeryTargetComponent>();
        while (query.MoveNext(out var patient, out _))
        {
            if (_ui.TryGetOpenUi<SurgeryBui>(patient, SurgeryUIKey.Key, out var bui))
                bui.InvalidateFishSurgery(ent.Owner);
        }
    }

    private void RefreshOpenFishUis()
    {
        var query = EntityQueryEnumerator<SurgeryTargetComponent>();
        while (query.MoveNext(out var patient, out _))
        {
            if (_ui.TryGetOpenUi<SurgeryBui>(patient, SurgeryUIKey.Key, out var bui))
                bui.QueueFishUiRefresh();
        }
    }
}
