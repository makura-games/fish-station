using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Skills;

/// <summary>
/// Диалог необратимого Confirm: Accept активируется через 3 секунды.
/// </summary>
public sealed class SkillConfirmDialog : DefaultWindow
{
    private readonly IGameTiming _timing;
    private readonly Label _body;
    private readonly Label _timer;
    private readonly Button _accept;
    private readonly Button _cancel;

    private TimeSpan _deadline;
    private bool _armed;

    public event Action? OnAccepted;

    public SkillConfirmDialog()
    {
        _timing = IoCManager.Resolve<IGameTiming>();

        Title = Loc.GetString("skills-confirm-dialog-title");
        MinSize = new Vector2(420, 200);
        SetSize = new Vector2(460, 230);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 10,
            Margin = new Thickness(12),
        };

        _body = new Label
        {
            HorizontalExpand = true,
            Align = Label.AlignMode.Left,
        };

        _timer = new Label
        {
            HorizontalExpand = true,
            Align = Label.AlignMode.Center,
            StyleClasses = { StyleClass.LabelHeading },
        };

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 8,
        };

        _cancel = new Button
        {
            Text = Loc.GetString("skills-confirm-dialog-cancel"),
            HorizontalExpand = true,
            MinHeight = 36,
        };
        _cancel.OnPressed += _ => Close();

        _accept = new Button
        {
            Text = Loc.GetString("skills-confirm-dialog-accept"),
            HorizontalExpand = true,
            MinHeight = 36,
            Disabled = true,
        };
        _accept.AddStyleClass(StyleNano.ButtonCaution);
        _accept.OnPressed += _ =>
        {
            if (!_armed)
                return;

            OnAccepted?.Invoke();
            Close();
        };

        buttons.AddChild(_cancel);
        buttons.AddChild(_accept);

        root.AddChild(_body);
        root.AddChild(_timer);
        root.AddChild(buttons);
        ContentsContainer.AddChild(root);
    }

    public void OpenFor(int remainingPoints, int totalPoints)
    {
        _body.Text = Loc.GetString("skills-confirm-dialog-body",
            ("remaining", remainingPoints),
            ("total", totalPoints));

        _armed = false;
        _accept.Disabled = true;
        _deadline = _timing.RealTime + TimeSpan.FromSeconds(3);
        UpdateTimer();
        OpenCentered();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdateTimer();
    }

    private void UpdateTimer()
    {
        var remaining = Math.Max(0, (int)Math.Ceiling((_deadline - _timing.RealTime).TotalSeconds));
        if (remaining > 0)
        {
            _timer.Text = Loc.GetString("skills-confirm-dialog-wait", ("seconds", remaining));
            _accept.Disabled = true;
            _armed = false;
            return;
        }

        _timer.Text = Loc.GetString("skills-confirm-dialog-ready");
        _accept.Disabled = false;
        _armed = true;
    }
}
