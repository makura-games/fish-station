using Content.Server.Administration.Managers;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._Fish.Administration;

/// <summary>
/// Подписывает админского призрака его админской должностью, чтобы его было видно среди других призраков.
/// </summary>
public sealed partial class AdminGhostTitleSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    /// <summary>
    /// Ставит призраку имя вида «должность имя», если у админа задана должность.
    /// </summary>
    public void ApplyAdminTitle(EntityUid ghost, ICommonSession player)
    {
        if (_admin.GetAdminData(player)?.Title is not { } title || string.IsNullOrWhiteSpace(title))
            return;

        var ghostName = _entities.GetComponent<MetaDataComponent>(ghost).EntityName;
        _metaData.SetEntityName(ghost, Loc.GetString("admin-ghost-name-with-title",
            ("title", title),
            ("name", ghostName)));
    }
}
