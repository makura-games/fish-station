using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared._Fish.Achievements;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Fish.Achievements.UI;

/// <summary>
/// Карточка достижения: иконка + переносимый заголовок + прогресс (без обрезки/наложения).
/// </summary>
public sealed class FishAchievementCard : ContainerButton
{
    private static readonly Color MutedColor = StyleNano.DisabledFore;
    private static readonly Color GoodColor = StyleNano.GoodGreenFore;
    private static readonly Color SelectedBorder = StyleNano.NanoGold;
    private static readonly ResPath FallbackIcon = new("/Textures/Interface/VerbIcons/information.svg.192dpi.png");

    public string AchievementId { get; private set; } = string.Empty;

    private readonly PanelContainer _panel;
    private readonly TextureRect _icon;
    private readonly RichTextLabel _title;
    private readonly ProgressBar _progress;
    private readonly Label _progressLabel;
    private readonly SpriteSystem _sprite;
    private bool _selected;

    public FishAchievementCard()
    {
        _sprite = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SpriteSystem>();

        HorizontalExpand = true;
        MinHeight = 72;
        StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };

        _panel = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };
        _panel.AddStyleClass(StyleClass.BackgroundPanelDark);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 10,
            Margin = new Thickness(10, 8),
            MouseFilter = MouseFilterMode.Ignore,
        };

        _icon = new TextureRect
        {
            MinSize = new Vector2(40, 40),
            MaxSize = new Vector2(40, 40),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            VerticalAlignment = VAlignment.Top,
            MouseFilter = MouseFilterMode.Ignore,
        };

        var textCol = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
            MouseFilter = MouseFilterMode.Ignore,
        };

        // RichTextLabel переносит длинные RU-названия вместо обрезки.
        _title = new RichTextLabel
        {
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };

        _progress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Visible = false,
            MinHeight = 8,
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };
        _progress.BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = StyleNano.PanelDark };
        _progress.ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = StyleNano.NanoGold };

        _progressLabel = new Label
        {
            FontColorOverride = MutedColor,
            Visible = false,
            HorizontalExpand = true,
            ClipText = false,
            MouseFilter = MouseFilterMode.Ignore,
        };

        textCol.AddChild(_title);
        textCol.AddChild(_progress);
        textCol.AddChild(_progressLabel);

        root.AddChild(_icon);
        root.AddChild(textCol);
        _panel.AddChild(root);
        AddChild(_panel);
    }

    public void Bind(AchievementPrototype proto, AchievementPlayerState state)
    {
        AchievementId = proto.ID;
        var unlocked = state.Unlocked;

        var titleColor = unlocked
            ? GoodColor
            : proto.Secret
                ? StyleNano.ConcerningOrangeFore
                : Color.White;
        _title.SetMessage(FormattedMessage.FromMarkupOrThrow(
            $"[color={titleColor.ToHex()}]{FormattedMessage.EscapeText(Loc.GetString(proto.Name))}[/color]"));

        SetIcon(proto, unlocked);

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
            _progressLabel.Visible = true;
            _progressLabel.Text = unlocked
                ? Loc.GetString("fish-achievements-status-unlocked")
                : proto.Secret
                    ? Loc.GetString("fish-achievements-status-secret")
                    : Loc.GetString("fish-achievements-status-locked");
            _progressLabel.FontColorOverride = unlocked ? GoodColor : MutedColor;
        }

        ApplyChrome();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        ApplyChrome();
    }

    private void SetIcon(AchievementPrototype proto, bool unlocked)
    {
        try
        {
            if (proto.Secret && !unlocked)
            {
                _icon.Texture = _sprite.Frame0(new SpriteSpecifier.Texture(FallbackIcon));
                _icon.Modulate = StyleNano.ConcerningOrangeFore;
                return;
            }

            if (proto.Icon is { } icon)
            {
                _icon.Texture = _sprite.Frame0(icon);
                _icon.Modulate = unlocked ? Color.White : new Color(180, 180, 180);
                return;
            }
        }
        catch
        {
            // fallback below
        }

        _icon.Texture = _sprite.Frame0(new SpriteSpecifier.Texture(FallbackIcon));
        _icon.Modulate = MutedColor;
    }

    private void ApplyChrome()
    {
        _panel.PanelOverride = _selected
            ? new StyleBoxFlat
            {
                BackgroundColor = StyleNano.PanelDark,
                BorderColor = SelectedBorder,
                BorderThickness = new Thickness(2),
            }
            : null;
    }
}
