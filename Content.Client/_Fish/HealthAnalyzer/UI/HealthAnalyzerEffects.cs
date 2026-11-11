using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.HealthAnalyzer.UI;

/// <summary>
/// Рисует лёгкие локальные эффекты поверх интерфейса анализатора здоровья.
/// </summary>
public sealed class HealthAnalyzerEffects : Control
{
    private const float ScanDuration = 2.5f;
    private const float ScanlineSpacing = 5f;

    private float _elapsed;
    private float _scanElapsed;
    private bool _scanActive;
    private bool _scanVisible;
    private Color _accentColor = HealthAnalyzerSheetlet.AccentColor;

    public HealthAnalyzerEffects()
    {
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    public void SetScanActive(bool scanActive)
    {
        _scanActive = scanActive;

        if (!scanActive)
            _scanVisible = false;
    }

    public void SetAccentColor(Color color)
    {
        _accentColor = color;
    }

    public void TriggerScan()
    {
        // Частые обновления данных не должны прерывать текущий проход полосы.
        if (!_scanActive || _scanVisible)
            return;

        _scanElapsed = 0f;
        _scanVisible = true;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _elapsed += args.DeltaSeconds;

        if (!_scanVisible)
            return;

        _scanElapsed += args.DeltaSeconds;
        if (_scanElapsed >= ScanDuration)
            _scanVisible = false;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var width = PixelWidth;
        var height = PixelHeight;
        if (width <= 0f || height <= 0f)
            return;

        DrawScanlines(handle, width, height);

        if (_scanVisible)
            DrawScan(handle, width, height);

        if (_scanActive)
            DrawActiveBorder(handle, width, height);
    }

    private void DrawScanlines(DrawingHandleScreen handle, float width, float height)
    {
        var spacing = Math.Max(3f, ScanlineSpacing * UIScale);
        var color = _accentColor.WithAlpha(0.018f);

        for (var y = spacing; y < height; y += spacing)
            handle.DrawLine(new Vector2(0f, y), new Vector2(width, y), color);
    }

    private void DrawScan(DrawingHandleScreen handle, float width, float height)
    {
        var progress = Math.Clamp(_scanElapsed / ScanDuration, 0f, 1f);
        var y = progress * height;
        var accent = _accentColor;

        handle.DrawRect(new UIBox2(0f, y - 10f, width, y + 10f), accent.WithAlpha(0.018f));
        handle.DrawRect(new UIBox2(0f, y - 3f, width, y + 3f), accent.WithAlpha(0.05f));
        handle.DrawLine(new Vector2(0f, y), new Vector2(width, y), accent.WithAlpha(0.48f));
    }

    private void DrawActiveBorder(DrawingHandleScreen handle, float width, float height)
    {
        var pulse = (MathF.Sin(_elapsed * 2.2f) + 1f) * 0.5f;
        var accent = _accentColor;

        DrawInsetBorder(handle, width, height, 1f, accent.WithAlpha(0.11f + pulse * 0.025f));
        DrawInsetBorder(handle, width, height, 2f, accent.WithAlpha(0.055f + pulse * 0.015f));
        DrawInsetBorder(handle, width, height, 3f, accent.WithAlpha(0.025f));
    }

    private static void DrawInsetBorder(
        DrawingHandleScreen handle,
        float width,
        float height,
        float inset,
        Color color)
    {
        handle.DrawRect(new UIBox2(inset, inset, width - inset, height - inset), color, false);
    }
}
