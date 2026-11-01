using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Robust.Client.Player;

namespace Content.Client.Cargo.UI;

public sealed partial class CargoConsoleMenu
{
    [Dependency] private IPlayerManager _fishPlayer = default!;
    [Dependency] private ILocalizationManager _fishLoc = default!;

    private AccessReaderSystem? _fishAccessReader;
    private bool? _fishApprovalAccess;

    /// <summary>Checks the same native ID reader used by the authoritative approval handler.</summary>
    private bool HasFishApprovalAccess()
    {
        _fishAccessReader ??= _entityManager.System<AccessReaderSystem>();
        if (_fishPlayer.LocalEntity is not { } user)
            return false;
        if (!_entityManager.TryGetComponent<AccessReaderComponent>(_owner, out var reader) || !reader.Enabled)
            return true;

        // Fish-edit: проверка интерфейса не должна записывать успешный доступ в журнал каждый кадр.
        var items = _fishAccessReader.FindPotentialAccessItems(user);
        var tags = _fishAccessReader.FindAccessTags(user, items);
        _fishAccessReader.FindStationRecordKeys(user, out var keys, items);
        return _fishAccessReader.IsAllowed(tags, keys, _owner, reader);
    }

    /// <summary>Initializes a newly created order row with the current user's approval access.</summary>
    private void SetFishApprovalAccess(CargoOrderRow row)
    {
        ApplyFishApprovalAccess(row, HasFishApprovalAccess());
    }

    /// <summary>Refreshes existing rows after an ID is equipped, removed or transferred.</summary>
    private void UpdateFishApprovalAccess()
    {
        var allowed = HasFishApprovalAccess();
        if (_fishApprovalAccess == allowed)
            return;

        _fishApprovalAccess = allowed;
        foreach (var control in Requests.Children)
        {
            if (control is CargoOrderRow row)
                ApplyFishApprovalAccess(row, allowed);
        }
    }

    private void ApplyFishApprovalAccess(CargoOrderRow row, bool allowed)
    {
        row.Approve.Disabled = !allowed;
        row.Approve.ToolTip = allowed ? null : _fishLoc.GetString("fish-cargo-approval-access-required");
    }
}
