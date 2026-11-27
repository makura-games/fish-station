using Robust.Client.UserInterface.Controls;

namespace Content.Client._Fish.UserInterface.FishUi;

/// <summary>
/// Степпер − / значение / + на обычных Button (клики не глотаются панелями).
/// </summary>
public sealed class FishUiStepper : BoxContainer
{
    public event Action? OnDecrease;
    public event Action? OnIncrease;

    private readonly Button _dec;
    private readonly Label _value;
    private readonly Button _inc;

    public FishUiStepper()
    {
        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 4;
        VerticalAlignment = VAlignment.Center;

        _dec = new Button
        {
            Text = "−",
            MinWidth = 40,
            MinHeight = 40,
        };
        _value = new Label
        {
            Text = "•",
            MinWidth = 44,
            Align = Label.AlignMode.Center,
            VerticalAlignment = VAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
        };
        _inc = new Button
        {
            Text = "+",
            MinWidth = 40,
            MinHeight = 40,
        };

        _dec.OnPressed += _ => OnDecrease?.Invoke();
        _inc.OnPressed += _ => OnIncrease?.Invoke();

        AddChild(_dec);
        AddChild(_value);
        AddChild(_inc);
    }

    public void SetState(string valueText, bool canDecrease, bool canIncrease, string? decreaseTip = null, string? increaseTip = null)
    {
        _value.Text = valueText;
        _dec.Disabled = !canDecrease;
        _inc.Disabled = !canIncrease;
        _dec.ToolTip = decreaseTip ?? string.Empty;
        _inc.ToolTip = increaseTip ?? string.Empty;
    }
}
