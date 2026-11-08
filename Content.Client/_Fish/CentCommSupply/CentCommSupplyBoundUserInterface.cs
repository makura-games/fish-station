using System;
using Content.Shared._Fish.CentCommSupply;
using Content.Shared.ActionBlocker;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.XAML;
using Robust.Client.Player;

namespace Content.Client._Fish.CentCommSupply;

public sealed partial class CentCommSupplyBoundUserInterface : BoundUserInterface
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private IPlayerManager _player = default!;

    private CentCommSupplyWindow? _window;
    private bool _orderPending;

    public CentCommSupplyBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CentCommSupplyWindow>();
        _window.OnStationSelected += (station, selectionId) =>
        {
            if (!CanInteract())
                return false;

            SendMessage(new CentCommSupplySelectStationMessage(station, selectionId));
            return true;
        };
        _window.OnOrderPressed += (station, entries) =>
        {
            if (_orderPending || !CanInteract())
                return false;

            _orderPending = true;
            SendMessage(new CentCommSupplyOrderMessage(Guid.NewGuid(), station, entries));
            return true;
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not CentCommSupplyBoundUserInterfaceState cast)
            return;

        if (cast.OrderPlaced || !string.IsNullOrWhiteSpace(cast.StatusMessage))
            _orderPending = false;

        _window?.UpdateState(cast);
    }

    /// <summary>
    /// Checks whether the local player can currently use the console.
    /// </summary>
    private bool CanInteract()
    {
        var localEntity = _player.LocalEntity;
        return localEntity is { } player && _actionBlocker.CanInteract(player, Owner);
    }
}
