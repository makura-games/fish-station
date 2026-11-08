using System;
using Content.Shared._Fish.CentCommSupply;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._Fish.CentCommSupply;

public sealed class CentCommSupplyBoundUserInterface : BoundUserInterface
{
    private CentCommSupplyWindow? _window;
    private bool _orderPending;

    public CentCommSupplyBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CentCommSupplyWindow>();
        _window.OnStationSelected += station =>
            SendMessage(new CentCommSupplySelectStationMessage(station));
        _window.OnOrderPressed += (station, entries) =>
        {
            if (_orderPending)
                return;

            _orderPending = true;
            SendMessage(new CentCommSupplyOrderMessage(Guid.NewGuid(), station, entries));
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
}
