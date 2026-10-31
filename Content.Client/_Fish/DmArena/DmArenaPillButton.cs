using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client._Fish.DmArena;

public sealed class DmArenaPillButton : PanelContainer
{
    public event Action? OnPressed;

    private readonly StyleBoxTexture _styleBox;
    private readonly Label _label;
    private bool _disabled;
    private bool _selected;
    private bool _hovered;

    public bool Disabled
    {
        get => _disabled;
        set
        {
            _disabled = value;
            UpdateVisual();
        }
    }

    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            UpdateVisual();
        }
    }

    public string Text
    {
        get => _label.Text ?? string.Empty;
        set => _label.Text = value;
    }

    public DmArenaPillButton()
    {
        MouseFilter = MouseFilterMode.Stop;
        HorizontalExpand = true;
        MinHeight = 45;

        _styleBox = DmArenaPalette.Rounded(DmArenaPalette.ButtonBg, 14, 8);
        PanelOverride = _styleBox;

        _label = new Label
        {
            Text = string.Empty,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            FontOverride = DmArenaPalette.GetFont(true, 12),
            FontColorOverride = DmArenaPalette.ButtonText,
        };
        AddChild(_label);

        OnKeyBindDown += OnClicked;
        OnMouseEntered += _ => SetHovered(true);
        OnMouseExited += _ => SetHovered(false);

        UpdateVisual();
    }

    private void OnClicked(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (!_disabled)
            OnPressed?.Invoke();

        args.Handle();
    }

    private void SetHovered(bool hovered)
    {
        _hovered = hovered;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (_disabled)
        {
            _styleBox.Modulate = DmArenaPalette.ButtonDisabledBg;
            _label.FontColorOverride = DmArenaPalette.ButtonDisabledFg;
            return;
        }

        _styleBox.Modulate = _selected
            ? DmArenaPalette.ButtonSelectedBg
            : _hovered ? DmArenaPalette.ButtonHoverBg : DmArenaPalette.ButtonBg;
        _label.FontColorOverride = DmArenaPalette.ButtonText;
    }
}
