using System.Numerics;
using System.Text;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Fish.Medical.Surgery;

/// <summary>Aligns a text arrow with the visible glyphs of its adjacent caption.</summary>
public sealed class FishSurgeryArrowLabel : Label
{
    public Control? ReferenceControl { get; set; }
    public string ReferenceText { get; set; } = string.Empty;

    private Font? _referenceFont;
    private string? _referenceText;
    private float _referenceScale;
    private float _inkCenter;

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (ReferenceControl is not { } reference || string.IsNullOrEmpty(Text) ||
            !TryGetStyleProperty<Font>(StylePropertyFont, out var font) ||
            !reference.TryGetStyleProperty<Font>(StylePropertyFont, out var referenceFont))
        {
            base.Draw(handle);
            return;
        }

        var rune = Rune.GetRuneAt(Text, 0);
        if (font.GetCharMetrics(rune, UIScale) is not { } arrow)
            return;

        if (_referenceFont != referenceFont || _referenceText != ReferenceText || _referenceScale != reference.UIScale)
        {
            _referenceFont = referenceFont;
            _referenceText = ReferenceText;
            _referenceScale = reference.UIScale;
            var top = int.MaxValue;
            var bottom = int.MinValue;
            foreach (var character in ReferenceText.EnumerateRunes())
            {
                if (referenceFont.GetCharMetrics(character, reference.UIScale) is not { Height: > 0 } glyph)
                    continue;
                top = Math.Min(top, -glyph.BearingY);
                bottom = Math.Max(bottom, glyph.Height - glyph.BearingY);
            }
            _inkCenter = top == int.MaxValue ? 0f : (top + bottom) / 2f;
        }

        // Совмещаем видимые глифы, а не прямоугольники строк с запасом под нижние выносные элементы.
        var baseline = reference.GlobalPixelPosition.Y - GlobalPixelPosition.Y + referenceFont.GetAscent(reference.UIScale);
        if (reference is Label)
            baseline += (reference.PixelHeight - referenceFont.GetHeight(reference.UIScale)) / 2;
        var y = baseline + _inkCenter + arrow.BearingY - arrow.Height / 2f;
        var x = (PixelWidth - arrow.Width) / 2f - arrow.BearingX;
        var color = TryGetStyleProperty<Color>(StylePropertyFontColor, out var styledColor) ? styledColor : Color.White;
        font.DrawChar(handle, rune, new Vector2(MathF.Round(x), MathF.Round(y)), UIScale, color);
    }
}
