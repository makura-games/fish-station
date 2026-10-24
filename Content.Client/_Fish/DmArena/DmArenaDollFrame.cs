using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._Fish.DmArena;

/// <summary>
/// Рамка с моделью персонажа, масштаб зависит от размера рамки.
/// </summary>
public sealed class DmArenaDollFrame : Control
{
    public SpriteView View { get; }

    public DmArenaDollFrame()
    {
        RectClipContent = true;
        HorizontalExpand = true;
        VerticalExpand = true;

        View = new SpriteView
        {
            OverrideDirection = Direction.South,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };
        AddChild(View);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        View.Measure(availableSize);
        return Vector2.Zero;
    }

    protected override void Resized()
    {
        base.Resized();

        // Целый масштаб, чтобы спрайт не плыл
        var scale = Math.Clamp((int) (Math.Min(Size.X, Size.Y) / 36f), 3, 12);
        View.Scale = new Vector2(scale, scale);
        // Размер не зависит от спрайта предметов
        View.SetSize = new Vector2(scale * 40, scale * 40);
    }
}
