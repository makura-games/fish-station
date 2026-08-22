using Content.Client._Fish.UserInterface.Crt;
using Content.Shared._Fish.Achievements;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using System.Numerics;

namespace Content.Client._Fish.Achievements.UI;

/// <summary>
/// Компактная карточка в сетке каталога.
/// </summary>
public sealed class FishAchievementCard : ContainerButton, IFishCrtThemedControl
{
    public string AchievementId { get; private set; } = string.Empty;

    private readonly FishCrtPanel _panel;
    private readonly FishCrtIcon _icon;
    private readonly FishCrtLabel _title;
    private readonly ProgressBar _progress;
    private readonly FishCrtLabel _progressLabel;
    private bool _selected;

    public FishAchievementCard()
    {
        MinSize = new Vector2(200, 96);
        StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };

        _panel = new FishCrtPanel
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Variant = FishCrtPanelVariant.Surface,
            Rounded = true,
            Effects = FishCrtEffects.None,
            BackgroundOpacity = 0.78f,
            BorderThickness = 1,
            MouseFilter = MouseFilterMode.Ignore,
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 10,
            Margin = new Thickness(10, 8),
        };

        _icon = new FishCrtIcon
        {
            IconState = FishCrtIcons.Medal,
            SetWidth = 36,
            SetHeight = 36,
            VerticalAlignment = VAlignment.Center,
        };

        var textColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 3,
        };

        _title = new FishCrtLabel
        {
            Heading = true,
            TextFontSize = 12,
        };

        _progress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Visible = false,
            MinHeight = 6,
            HorizontalExpand = true,
        };

        _progressLabel = new FishCrtLabel
        {
            TextFontSize = 10,
            Tone = FishCrtTone.Muted,
            Visible = false,
        };

        textColumn.AddChild(_title);
        textColumn.AddChild(_progress);
        textColumn.AddChild(_progressLabel);

        root.AddChild(_icon);
        root.AddChild(textColumn);
        _panel.AddChild(root);
        AddChild(_panel);
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        ApplyPanelStyle();
    }

    public void Bind(AchievementPrototype proto, AchievementPlayerState state)
    {
        AchievementId = proto.ID;
        var unlocked = state.Unlocked;

        _title.Text = Loc.GetString(proto.Name);
        _title.Tone = unlocked ? FishCrtTone.Good : FishCrtTone.Default;

        if (proto.Secret && !unlocked)
        {
            _icon.IconState = FishCrtIcons.Warning;
            _icon.Tone = FishCrtTone.Warning;
            _title.Tone = FishCrtTone.Warning;
        }
        else
        {
            _icon.IconState = FishCrtIcons.Medal;
            _icon.Tone = unlocked ? FishCrtTone.Good : FishCrtTone.Muted;
        }

        var target = System.Math.Max(1, proto.ProgressTarget);
        var showProgress = target > 1 && proto.Condition != AchievementConditionKeys.Manual;
        if (showProgress)
        {
            _progress.Visible = true;
            _progressLabel.Visible = true;
            _progress.MaxValue = target;
            _progress.Value = System.Math.Clamp(state.Progress, 0, target);
            _progressLabel.Text = $"{state.Progress}/{target}";
            _progressLabel.Tone = unlocked ? FishCrtTone.Good : FishCrtTone.Muted;
        }
        else
        {
            _progress.Visible = false;
            _progressLabel.Visible = false;
        }

        ApplyPanelStyle();
    }

    void IFishCrtThemedControl.ApplyCrtTheme(FishCrtThemeContext context)
    {
        _icon.ApplyAppearance(context);
        _title.ApplyAppearance(context);
        _progressLabel.ApplyAppearance(context);
        ApplyPanelStyle(context);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        ((IFishCrtThemedControl) this).ApplyCrtTheme(FishCrtThemeHelpers.FindContext(this));
    }

    private void ApplyPanelStyle(FishCrtThemeContext? context = null)
    {
        context ??= FishCrtThemeHelpers.FindContext(this);
        var palette = context.Value.Palette;

        _panel.BackgroundOpacity = _selected ? 0.92f : 0.72f;
        _panel.BorderThickness = _selected ? 2 : 1;
        _panel.SetColorOverrides(
            null,
            _selected ? palette.Border : palette.Muted.WithAlpha(0.35f));

        _progress.BackgroundStyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = palette.Background.WithAlpha(0.95f),
        };
        _progress.ForegroundStyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = palette.Good,
        };
    }
}
