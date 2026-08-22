using System;
using System.Collections.Generic;
using Content.Client._Fish.UserInterface.Crt;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client._Fish.Achievements.UI;

/// <summary>
/// Всплывающее уведомление об unlock (стек снизу справа).
/// </summary>
public sealed class FishAchievementUnlockToast : PanelContainer
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(5);

    private readonly FishCrtPanel _accent;
    private readonly FishCrtIcon _icon;
    private readonly FishCrtLabel _title;
    private readonly FishCrtLabel _subtitle;
    private readonly PanelContainer _card;
    private TimeSpan _spawnTime;

    public FishAchievementUnlockToast(string title, string subtitle)
    {
        MinSize = new Vector2(360, 88);
        MaxSize = new Vector2(420, 96);
        Margin = new Thickness(0, 0, 16, 10);

        _card = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var palette = FishCrtPalettes.Get(FishCrtPalettePreset.Station);
        _card.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.InterpolateBetween(palette.Background, palette.Fill, 0.35f),
            BorderColor = palette.Border.WithAlpha(0.55f),
            BorderThickness = new Thickness(1),
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 0,
        };

        _accent = new FishCrtPanel
        {
            Variant = FishCrtPanelVariant.Surface,
            Effects = FishCrtEffects.None,
            BackgroundOpacity = 1f,
            BorderThickness = 0,
            MinWidth = 6,
            MaxWidth = 6,
            VerticalExpand = true,
        };
        _accent.SetColorOverrides(palette.Good, null);

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 12,
            Margin = new Thickness(12, 10),
        };

        _icon = new FishCrtIcon
        {
            IconState = FishCrtIcons.Medal,
            SetWidth = 48,
            SetHeight = 48,
            Tone = FishCrtTone.Good,
            VerticalAlignment = VAlignment.Center,
        };
        _icon.ApplyAppearance(new FishCrtThemeContext(palette, new FishCrtAppearanceSettings(true, false)));

        var textColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 2,
        };

        _title = new FishCrtLabel
        {
            Heading = true,
            TextFontSize = 14,
            Text = title,
            Tone = FishCrtTone.Good,
        };
        _title.ApplyAppearance(new FishCrtThemeContext(palette, new FishCrtAppearanceSettings(true, false)));

        _subtitle = new FishCrtLabel
        {
            TextFontSize = 11,
            Text = subtitle,
            Tone = FishCrtTone.Muted,
        };
        _subtitle.ApplyAppearance(new FishCrtThemeContext(palette, new FishCrtAppearanceSettings(true, false)));

        textColumn.AddChild(_title);
        textColumn.AddChild(_subtitle);

        body.AddChild(_icon);
        body.AddChild(textColumn);

        row.AddChild(_accent);
        row.AddChild(body);
        _card.AddChild(row);
        AddChild(_card);
    }

    public void MarkSpawned(TimeSpan now) => _spawnTime = now;

    public bool IsExpired(TimeSpan now) => now - _spawnTime >= Lifetime;

    public float GetFade(TimeSpan now)
    {
        var elapsed = now - _spawnTime;
        if (elapsed < TimeSpan.FromMilliseconds(250))
            return (float) (elapsed.TotalMilliseconds / 250f);

        var remaining = Lifetime - elapsed;
        if (remaining < TimeSpan.FromMilliseconds(600))
            return (float) (remaining.TotalMilliseconds / 600d);

        return 1f;
    }
}

/// <summary>
/// Контейнер тостов поверх UI.
/// </summary>
public sealed class FishAchievementToastHost : Control
{
    private const int MaxVisible = 4;

    private readonly BoxContainer _stack;
    private readonly List<FishAchievementUnlockToast> _toasts = new();
    private readonly IGameTiming _timing;

    public FishAchievementToastHost(IGameTiming timing)
    {
        _timing = timing;
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalExpand = true;
        VerticalExpand = true;

        _stack = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalAlignment = VAlignment.Bottom,
            HorizontalAlignment = HAlignment.Right,
            Margin = new Thickness(0, 0, 8, 24),
        };
        AddChild(_stack);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        for (var i = _toasts.Count - 1; i >= 0; i--)
        {
            var toast = _toasts[i];
            if (toast.IsExpired(_timing.CurTime))
            {
                _stack.RemoveChild(toast);
                _toasts.RemoveAt(i);
                continue;
            }

            toast.Modulate = Color.White.WithAlpha(toast.GetFade(_timing.CurTime));
        }
    }

    public void Push(string title, string subtitle)
    {
        while (_toasts.Count >= MaxVisible)
        {
            var oldest = _toasts[0];
            _stack.RemoveChild(oldest);
            _toasts.RemoveAt(0);
        }

        var toast = new FishAchievementUnlockToast(title, subtitle);
        toast.MarkSpawned(_timing.CurTime);
        _toasts.Add(toast);
        _stack.AddChild(toast);
    }
}
