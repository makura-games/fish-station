using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._Fish.Arcade.Minesweeper;

public sealed class MinesweeperCellButton : ContainerButton
{
    /// <summary>
    ///     Насколько светлее становится закрытая клетка под курсором.
    /// </summary>
    private const float HoverLighten = 0.15f;

    /// <summary>
    ///     Координата клетки по горизонтали.
    /// </summary>
    public readonly int X;

    /// <summary>
    ///     Координата клетки по вертикали.
    /// </summary>
    public readonly int Y;

    /// <summary>
    ///     Плоская заливка без рамок и отступов: иконка получает всю клетку целиком и рисуется
    ///     без дробного масштабирования, а сетку рисуют промежутки между клетками.
    /// </summary>
    private readonly StyleBoxFlat _box = new();

    private readonly Label _text;
    private readonly TextureRect _icon;

    private Color _color;
    private bool _hovered;

    /// <summary>
    ///     Игрок нажал ПКМ по клетке.
    /// </summary>
    public event Action<MinesweeperCellButton>? OnRightPressed;

    public MinesweeperCellButton(int x, int y, Color color)
    {
        X = x;
        Y = y;
        _color = color;

        StyleBoxOverride = _box;
        _box.BackgroundColor = color;

        _text = new Label
        {
            StyleClasses = { StyleClass.LabelHeading },
            Align = Label.AlignMode.Center,
            VAlign = Label.VAlignMode.Center,
            MouseFilter = MouseFilterMode.Ignore,
        };
        AddChild(_text);

        _icon = new TextureRect
        {
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            MouseFilter = MouseFilterMode.Ignore,
            CanShrink = true,
            Visible = false,
        };
        AddChild(_icon);

        OnMouseEntered += _ =>
        {
            _hovered = true;
            UpdateColor();
        };

        OnMouseExited += _ =>
        {
            _hovered = false;
            UpdateColor();
        };
    }

    /// <summary>
    ///     Задаёт цвет клетки: закрытая, открытая или подсвеченная после подрыва.
    /// </summary>
    public void SetColor(Color color)
    {
        _color = color;
        UpdateColor();
    }

    /// <summary>
    ///     Показывает в клетке цифру. Пустая строка очищает клетку.
    /// </summary>
    public void SetText(string text, Color color)
    {
        _icon.Visible = false;
        _text.Visible = true;
        _text.Text = text;
        _text.FontColorOverride = color;
    }

    /// <summary>
    ///     Показывает в клетке иконку. Иконка вписывается в клетку с сохранением пропорций.
    /// </summary>
    public void SetIcon(Texture texture)
    {
        _text.Visible = false;
        _text.Text = string.Empty;

        _icon.Visible = true;
        _icon.Texture = texture;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (!Disabled && args.Function == EngineKeyFunctions.UIRightClick)
        {
            OnRightPressed?.Invoke(this);
            args.Handle();
            return;
        }

        base.KeyBindDown(args);
    }

    private void UpdateColor()
    {
        _box.BackgroundColor = _hovered
            ? Color.InterpolateBetween(_color, Color.White, HoverLighten)
            : _color;
    }
}
