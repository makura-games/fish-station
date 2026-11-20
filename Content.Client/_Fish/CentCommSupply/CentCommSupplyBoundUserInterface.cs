using System;
using Content.Shared._Fish.CentCommSupply;
using Content.Shared.ActionBlocker;
using Robust.Client.UserInterface;
using Robust.Client.Player;

namespace Content.Client._Fish.CentCommSupply;

public sealed partial class CentCommSupplyBoundUserInterface : BoundUserInterface
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private IPlayerManager _player = default!;

    private CentCommSupplyWindow? _window;
    private CentCommSupplyUserStateMessage? _privateState;

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
        _window.OnOrderPressed += (orderId, station, entries) =>
        {
            if (!CanInteract())
                return false;

            SendMessage(new CentCommSupplyOrderMessage(orderId, station, entries));
            return true;
        };
        if (_privateState is { } state)
            _window.UpdateState(state);

        SendMessage(new CentCommSupplyStateRequestMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_privateState == null && state is CentCommSupplyCatalogState catalog)
            _window?.UpdateState(catalog.Catalog);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (message is CentCommSupplyUserStateMessage state)
        {
            _privateState = state;
            _window?.UpdateState(state);
        }
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
