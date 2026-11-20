using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared._Fish.Achievements;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Fish.Achievements.UI;

/// <summary>
/// Правая панель описания: иконка, статус, полное описание без обрезки.
/// </summary>
public sealed class FishAchievementDetailPane : PanelContainer
{
    private static readonly Color MutedColor = StyleNano.DisabledFore;
    private static readonly Color GoodColor = StyleNano.GoodGreenFore;
    private static readonly ResPath FallbackIcon = new("/Textures/Interface/VerbIcons/information.svg.192dpi.png");

    private readonly SpriteSystem _sprite;
    private readonly TextureRect _icon;
    private readonly RichTextLabel _title;
    private readonly Label _status;
    private readonly RichTextLabel _description;
    private readonly ProgressBar _progress;
    private readonly Label _progressLabel;
    private readonly Label _placeholder;
    private readonly BoxContainer _content;

    public FishAchievementDetailPane()
    {
        _sprite = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SpriteSystem>();

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
            ClipText = false,
        };

        _content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
            Visible = false,
        };

        _icon = new TextureRect
        {
            MinSize = new Vector2(72, 72),
            MaxSize = new Vector2(72, 72),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            HorizontalAlignment = HAlignment.Center,
        };

        _title = new RichTextLabel
        {
            HorizontalAlignment = HAlignment.Center,
            HorizontalExpand = true,
        };

        _status = new Label
        {
            FontColorOverride = MutedColor,
            HorizontalAlignment = HAlignment.Center,
            ClipText = false,
        };

        _description = new RichTextLabel
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
        _progress.BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = StyleNano.PanelDark };
        _progress.ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = StyleNano.NanoGold };

        _progressLabel = new Label
        {
            FontColorOverride = MutedColor,
            Visible = false,
            ClipText = false,
        };

        _content.AddChild(_icon);
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
        _title.SetMessage(FormattedMessage.FromMarkupOrThrow(
            $"[bold]{FormattedMessage.EscapeText(Loc.GetString(proto.Name))}[/bold]"));

        try
        {
            if (proto.Secret && !unlocked)
            {
                _icon.Texture = _sprite.Frame0(new SpriteSpecifier.Texture(FallbackIcon));
                _icon.Modulate = StyleNano.ConcerningOrangeFore;
            }
            else if (proto.Icon is { } icon)
            {
                _icon.Texture = _sprite.Frame0(icon);
                _icon.Modulate = unlocked ? Color.White : new Color(180, 180, 180);
            }
            else
            {
                _icon.Texture = _sprite.Frame0(new SpriteSpecifier.Texture(FallbackIcon));
                _icon.Modulate = MutedColor;
            }
        }
        catch
        {
            _icon.Texture = _sprite.Frame0(new SpriteSpecifier.Texture(FallbackIcon));
            _icon.Modulate = MutedColor;
        }

        if (proto.Secret && !unlocked)
        {
            _description.SetMessage(FormattedMessage.FromMarkupOrThrow(
                FormattedMessage.EscapeText(
                    Loc.GetString(proto.SecretDescription ?? "fish-achievements-secret-placeholder"))));
            _status.Text = Loc.GetString("fish-achievements-status-secret");
            _status.FontColorOverride = StyleNano.ConcerningOrangeFore;
        }
        else
        {
            _description.SetMessage(FormattedMessage.FromMarkupOrThrow(
                FormattedMessage.EscapeText(Loc.GetString(proto.Description))));
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
