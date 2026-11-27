using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared._Fish.Achievements;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Fish.Achievements.UI;

/// <summary>
/// Карточка достижения в сетке каталога (Nano, без CRT).
/// </summary>
public sealed class FishAchievementCard : ContainerButton
{
    private static readonly Color MutedColor = StyleNano.DisabledFore;
    private static readonly Color GoodColor = StyleNano.GoodGreenFore;
    private static readonly Color SelectedBorder = StyleNano.NanoGold;

    public string AchievementId { get; private set; } = string.Empty;

    private readonly PanelContainer _panel;
    private readonly Label _title;
    private readonly ProgressBar _progress;
    private readonly Label _progressLabel;
    private bool _selected;

    public FishAchievementCard()
    {
        MinSize = new Vector2(200, 88);
        StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };

        _panel = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
            StyleClasses = { StyleClass.BackgroundPanelDark },
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 4,
            Margin = new Thickness(10, 8),
            MouseFilter = MouseFilterMode.Ignore,
        };

        _title = new Label
        {
            StyleClasses = { StyleClass.LabelHeading },
            ClipText = true,
            MouseFilter = MouseFilterMode.Ignore,
        };

        _progress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Visible = false,
            MinHeight = 6,
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };

        _progressLabel = new Label
        {
            FontColorOverride = MutedColor,
            Visible = false,
            MouseFilter = MouseFilterMode.Ignore,
        };

        root.AddChild(_title);
        root.AddChild(_progress);
        root.AddChild(_progressLabel);
        _panel.AddChild(root);
        AddChild(_panel);
    }

    public void Bind(AchievementPrototype proto, AchievementPlayerState state)
    {
        AchievementId = proto.ID;
        var unlocked = state.Unlocked;

        _title.Text = Loc.GetString(proto.Name);
        _title.FontColorOverride = unlocked ? GoodColor : null;

        if (proto.Secret && !unlocked)
            _title.FontColorOverride = StyleNano.ConcerningOrangeFore;

        var target = Math.Max(1, proto.ProgressTarget);
        var showProgress = target > 1 && proto.Condition != AchievementConditionKeys.Manual;
        if (showProgress)
        {
            _progress.Visible = true;
            _progressLabel.Visible = true;
            _progress.MaxValue = target;
            _progress.Value = Math.Clamp(state.Progress, 0, target);
            _progressLabel.Text = $"{state.Progress}/{target}";
            _progressLabel.FontColorOverride = unlocked ? GoodColor : MutedColor;
        }
        else
        {
            _progress.Visible = false;
            _progressLabel.Visible = false;
        }

        ApplyChrome();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        ApplyChrome();
    }

    private void ApplyChrome()
    {
        if (_selected)
        {
            _panel.PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = StyleNano.PanelDark,
                BorderColor = SelectedBorder,
                BorderThickness = new Thickness(2),
            };
        }
        else
        {
            _panel.PanelOverride = null;
            if (!_panel.HasStyleClass(StyleClass.BackgroundPanelDark))
                _panel.AddStyleClass(StyleClass.BackgroundPanelDark);
        }
    }
}
