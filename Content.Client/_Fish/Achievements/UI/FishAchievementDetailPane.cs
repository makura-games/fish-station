using Content.Client.Stylesheets;
using Content.Shared._Fish.Achievements;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Fish.Achievements.UI;

/// <summary>
/// Правая панель описания достижения (Nano, без CRT).
/// </summary>
public sealed class FishAchievementDetailPane : PanelContainer
{
    private static readonly Color MutedColor = StyleNano.DisabledFore;
    private static readonly Color GoodColor = StyleNano.GoodGreenFore;

    private readonly Label _title;
    private readonly Label _status;
    private readonly Label _description;
    private readonly ProgressBar _progress;
    private readonly Label _progressLabel;
    private readonly Label _placeholder;
    private readonly BoxContainer _content;

    public FishAchievementDetailPane()
    {
        AddStyleClass(StyleClass.PanelDark);
        HorizontalExpand = true;
        VerticalExpand = true;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 10,
            Margin = new Thickness(16, 14),
        };

        _placeholder = new Label
        {
            Text = Loc.GetString("fish-achievements-detail-hint"),
            FontColorOverride = MutedColor,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        _content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
            Visible = false,
        };

        _title = new Label
        {
            StyleClasses = { StyleClass.LabelHeading },
            HorizontalAlignment = HAlignment.Center,
        };

        _status = new Label
        {
            FontColorOverride = MutedColor,
            HorizontalAlignment = HAlignment.Center,
        };

        _description = new Label
        {
            HorizontalExpand = true,
        };

        _progress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Visible = false,
            MinHeight = 10,
            HorizontalExpand = true,
        };

        _progressLabel = new Label
        {
            FontColorOverride = MutedColor,
            Visible = false,
        };

        _content.AddChild(_title);
        _content.AddChild(_status);
        _content.AddChild(_description);
        _content.AddChild(_progress);
        _content.AddChild(_progressLabel);

        root.AddChild(_placeholder);
        root.AddChild(_content);
        AddChild(root);

        ShowPlaceholder();
    }

    public void ShowPlaceholder()
    {
        _placeholder.Visible = true;
        _content.Visible = false;
        _progress.Visible = false;
        _progressLabel.Visible = false;
    }

    public void Bind(AchievementPrototype proto, AchievementPlayerState state)
    {
        _placeholder.Visible = false;
        _content.Visible = true;

        var unlocked = state.Unlocked;
        _title.Text = Loc.GetString(proto.Name);

        if (proto.Secret && !unlocked)
        {
            _description.Text = Loc.GetString(proto.SecretDescription ?? "fish-achievements-secret-placeholder");
            _status.Text = Loc.GetString("fish-achievements-status-secret");
            _status.FontColorOverride = StyleNano.ConcerningOrangeFore;
        }
        else
        {
            _description.Text = Loc.GetString(proto.Description);
            _status.Text = unlocked
                ? Loc.GetString("fish-achievements-status-unlocked")
                : Loc.GetString("fish-achievements-status-locked");
            _status.FontColorOverride = unlocked ? GoodColor : MutedColor;
        }

        var target = Math.Max(1, proto.ProgressTarget);
        var showProgress = target > 1 && proto.Condition != AchievementConditionKeys.Manual;
        if (showProgress)
        {
            _progress.Visible = true;
            _progressLabel.Visible = true;
            _progress.MaxValue = target;
            _progress.Value = Math.Clamp(state.Progress, 0, target);
            _progressLabel.Text = Loc.GetString(
                "fish-achievements-progress-count",
                ("current", state.Progress),
                ("target", target));
            _progressLabel.FontColorOverride = unlocked ? GoodColor : MutedColor;
        }
        else
        {
            _progress.Visible = false;
            _progressLabel.Visible = false;
        }
    }
}
