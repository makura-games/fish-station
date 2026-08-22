using Content.Shared.Atmos.Piping.Trinary.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Fish.Atmos.FIlter;

[UsedImplicitly]
public sealed class GasRangeTemperatureFilterBoundUserInterface : BoundUserInterface
{
    private GasRangeTemperatureFilterWindow? _window;

    public GasRangeTemperatureFilterBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<GasRangeTemperatureFilterWindow>();

        _window.ToggleStatusPressed += () => SendMessage(new GasRangeTemperatureFilterToggleStatusMessage(_window.FilterStatus));

        // Используем новое событие TransferRateSet
        _window.TransferRateSet += rate =>
        {
            SendMessage(new GasRangeTemperatureFilterChangeRateMessage(rate));
        };

        _window.RangeChanged += (low, high) =>
        {
            SendMessage(new GasRangeTemperatureFilterChangeRangeMessage(low, high));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null || state is not GasRangeTemperatureFilterBoundUserInterfaceState cast)
            return;

        _window.SetState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _window?.Dispose();
    }
}