using Content.Client._Fish.UserInterface.Crt;
using Content.Shared._Fish.Achievements;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Fish.Achievements.UI;

/// <summary>
/// Правая панель с описанием выбранного достижения.
/// </summary>
public sealed class FishAchievementDetailPane : PanelContainer
{
    private readonly FishCrtPanel _shell;
    private readonly FishCrtIcon _icon;
    private readonly FishCrtLabel _title;
    private readonly FishCrtLabel _status;
    private readonly FishCrtLabel _description;
    private readonly ProgressBar _progress;
    private readonly FishCrtLabel _progressLabel;
    private readonly FishCrtLabel _placeholder;

    public FishAchievementDetailPane()
    {
        _shell = new FishCrtPanel
        {
            Variant = FishCrtPanelVariant.Inset,
            Rounded = true,
            Effects = FishCrtEffects.None,
            BackgroundOpacity = 0.55f,
            BorderThickness = 0,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 10,
            Margin = new Thickness(16, 14),
        };

        var iconFrame = new FishCrtPanel
        {
            Variant = FishCrtPanelVariant.Surface,
            Rounded = true,
            Effects = FishCrtEffects.None,
            BackgroundOpacity = 0.75f,
            BorderThickness = 0,
            HorizontalAlignment = HAlignment.Center,
        };

        _icon = new FishCrtIcon
        {
            IconState = FishCrtIcons.Medal,
            SetWidth = 72,
            SetHeight = 72,
            Margin = new Thickness(18),
        };
        iconFrame.AddChild(_icon);

        _title = new FishCrtLabel
        {
            Heading = true,
            TextFontSize = 18,
            HorizontalAlignment = HAlignment.Center,
        };

        _status = new FishCrtLabel
        {
            TextFontSize = 11,
            Tone = FishCrtTone.Muted,
            HorizontalAlignment = HAlignment.Center,
        };

        var divider = new FishCrtSeparator { Orientation = FishCrtSeparatorOrientation.Horizontal };

        _description = new FishCrtLabel
        {
            HorizontalExpand = true,
            TextFontSize = 12,
        };

        _progress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Visible = false,
            MinHeight = 10,
            HorizontalExpand = true,
            Margin = new Thickness(0, 4, 0, 0),
        };

        _progressLabel = new FishCrtLabel
        {
            TextFontSize = 11,
            Tone = FishCrtTone.Muted,
            Visible = false,
        };

        _placeholder = new FishCrtLabel
        {
            Text = Loc.GetString("fish-achievements-detail-hint"),
            Tone = FishCrtTone.Muted,
            TextFontSize = 13,
            HorizontalExpand = true,
            VerticalExpand = true,
            VerticalAlignment = VAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
        };

        root.AddChild(iconFrame);
        root.AddChild(_title);
        root.AddChild(_status);
        root.AddChild(divider);
        root.AddChild(_description);
        root.AddChild(_progress);
        root.AddChild(_progressLabel);
        root.AddChild(_placeholder);

        _shell.AddChild(root);
        AddChild(_shell);
        ShowPlaceholder();
    }

    public void ShowPlaceholder()
    {
        _placeholder.Visible = true;
        _icon.Visible = false;
        _title.Visible = false;
        _status.Visible = false;
        _description.Visible = false;
        _progress.Visible = false;
        _progressLabel.Visible = false;
    }

    public void Bind(AchievementPrototype proto, AchievementPlayerState state)
    {
        _placeholder.Visible = false;
        _icon.Visible = true;
        _title.Visible = true;
        _status.Visible = true;
        _description.Visible = true;

        var unlocked = state.Unlocked;
        _title.Text = Loc.GetString(proto.Name);
        _title.Tone = unlocked ? FishCrtTone.Good : FishCrtTone.Default;

        if (proto.Secret && !unlocked)
        {
            _icon.IconState = FishCrtIcons.Warning;
            _icon.Tone = FishCrtTone.Warning;
            _description.Text = Loc.GetString(proto.SecretDescription ?? "fish-achievements-secret-placeholder");
            _description.Tone = FishCrtTone.Warning;
            _status.Text = Loc.GetString("fish-achievements-status-secret");
            _status.Tone = FishCrtTone.Warning;
        }
        else
        {
            _icon.IconState = FishCrtIcons.Medal;
            _icon.Tone = unlocked ? FishCrtTone.Good : FishCrtTone.Muted;
            _description.Text = Loc.GetString(proto.Description);
            _description.Tone = FishCrtTone.Default;
            _status.Text = unlocked
                ? Loc.GetString("fish-achievements-status-unlocked")
                : Loc.GetString("fish-achievements-status-locked");
            _status.Tone = unlocked ? FishCrtTone.Good : FishCrtTone.Muted;
        }

        var target = System.Math.Max(1, proto.ProgressTarget);
        var showProgress = target > 1 && proto.Condition != AchievementConditionKeys.Manual;
        if (showProgress)
        {
            _progress.Visible = true;
            _progressLabel.Visible = true;
            _progress.MaxValue = target;
            _progress.Value = System.Math.Clamp(state.Progress, 0, target);
            _progressLabel.Text = Loc.GetString(
                "fish-achievements-progress-count",
                ("current", state.Progress),
                ("target", target));
            _progressLabel.Tone = unlocked ? FishCrtTone.Good : FishCrtTone.Muted;

            var palette = FishCrtThemeHelpers.FindContext(this).Palette;
            _progress.BackgroundStyleBoxOverride = new StyleBoxFlat
            {
                BackgroundColor = palette.Background.WithAlpha(0.95f),
            };
            _progress.ForegroundStyleBoxOverride = new StyleBoxFlat
            {
                BackgroundColor = unlocked ? palette.Good : palette.Border,
            };
        }
        else
        {
            _progress.Visible = false;
            _progressLabel.Visible = false;
        }
    }
}
