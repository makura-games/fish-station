using System.Numerics;
using System.Text;
using Content.Client.Stylesheets;
using Content.Shared._Fish.Skills;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client._Fish.Skills;

/// <summary>
/// Окно распределения навыков: Nano-стиль, степпер на обычных Button, Confirm через диалог.
/// </summary>
public sealed class SkillWindow : DefaultWindow
{
    private static readonly Color GoodColor = StyleNano.GoodGreenFore;
    private static readonly Color MutedColor = StyleNano.DisabledFore;
    private static readonly Color WarningColor = StyleNano.ConcerningOrangeFore;
    private static readonly Color DangerColor = StyleNano.DangerousRedFore;

    public event Action<string>? OnIncrease;
    public event Action<string>? OnDecrease;
    public event Action? OnConfirm;
    public event Action? OnReset;

    private readonly Label _header;
    private readonly Label _help;
    private readonly Label _points;
    private readonly Label _lockedHint;
    private readonly Label _status;
    private readonly BoxContainer _skillsList;
    private readonly Button _reset;
    private readonly Button _confirm;

    private readonly Dictionary<string, int> _levels = new();
    private readonly IPrototypeManager _prototypes;
    private readonly SpriteSystem _sprite;

    private SkillConfirmDialog? _confirmDialog;
    private int _remainingPoints;
    private int _totalPoints;
    private readonly Dictionary<string, int> _confirmSnapshotLevels = new();
    private int _confirmSnapshotRemaining = -1;
    private int _confirmSnapshotTotal = -1;

    public SkillWindow()
    {
        _prototypes = IoCManager.Resolve<IPrototypeManager>();
        _sprite = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SpriteSystem>();

        Title = Loc.GetString("skills-window-title");
        MinSize = new Vector2(720, 600);
        SetSize = new Vector2(760, 660);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
            Margin = new Thickness(12, 8, 12, 12),
        };

        var headerPanel = new PanelContainer
        {
            HorizontalExpand = true,
            StyleClasses = { StyleClass.BackgroundPanelDark },
        };

        var headerInner = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 6,
            Margin = new Thickness(12, 10),
        };

        _header = new Label
        {
            Text = Loc.GetString("skills-window-header"),
            StyleClasses = { StyleClass.LabelHeading },
            HorizontalExpand = true,
        };

        _help = new Label
        {
            Text = Loc.GetString("skills-window-help"),
            FontColorOverride = MutedColor,
            HorizontalExpand = true,
        };

        var pointsRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 8,
        };

        pointsRow.AddChild(new Label
        {
            Text = Loc.GetString("skills-window-points"),
            FontColorOverride = MutedColor,
            VerticalAlignment = VAlignment.Center,
        });

        _points = new Label
        {
            Text = Loc.GetString("skills-window-loading"),
            StyleClasses = { StyleClass.LabelHeading },
            VerticalAlignment = VAlignment.Center,
        };

        _lockedHint = new Label
        {
            Visible = false,
            FontColorOverride = WarningColor,
            HorizontalAlignment = HAlignment.Right,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        };

        pointsRow.AddChild(_points);
        pointsRow.AddChild(_lockedHint);

        headerInner.AddChild(_header);
        headerInner.AddChild(_help);
        headerInner.AddChild(pointsRow);
        headerPanel.AddChild(headerInner);
        root.AddChild(headerPanel);

        _status = new Label
        {
            Visible = false,
            FontColorOverride = MutedColor,
            HorizontalExpand = true,
        };
        root.AddChild(_status);

        var listPanel = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            StyleClasses = { StyleClass.PanelDark },
        };

        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            MinHeight = 360,
            Margin = new Thickness(8),
        };

        _skillsList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 6,
        };

        scroll.AddChild(_skillsList);
        listPanel.AddChild(scroll);
        root.AddChild(listPanel);

        var footer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 10,
            MinHeight = 40,
        };

        _reset = new Button
        {
            Text = Loc.GetString("skills-window-reset"),
            ToolTip = Loc.GetString("skills-window-reset-tooltip"),
            HorizontalExpand = true,
            MinHeight = 38,
            Disabled = true,
        };
        _reset.OnPressed += _ => OnReset?.Invoke();

        _confirm = new Button
        {
            Text = Loc.GetString("skills-window-confirm"),
            ToolTip = Loc.GetString("skills-window-confirm-tooltip"),
            HorizontalExpand = true,
            MinHeight = 38,
            Disabled = true,
        };
        _confirm.AddStyleClass(StyleNano.ButtonCaution);
        _confirm.OnPressed += _ => OpenConfirmDialog();

        _skillsList.AddChild(new Label
        {
            Text = Loc.GetString("skills-window-loading"),
            FontColorOverride = MutedColor,
            HorizontalExpand = true,
            Margin = new Thickness(0, 24, 0, 0),
        });

        footer.AddChild(_reset);
        footer.AddChild(_confirm);
        root.AddChild(footer);

        ContentsContainer.AddChild(root);
        OnClose += CloseConfirmDialog;
    }

    public int GetLevel(string skillId) => _levels.GetValueOrDefault(skillId, SkillLevels.Unskilled);

    public void UpdateState(SkillEuiState state)
    {
        try
        {
            ApplyState(state);
        }
        catch (Exception e)
        {
            _skillsList.RemoveAllChildren();
            _skillsList.AddChild(new Label
            {
                Text = Loc.GetString("skills-window-render-error", ("error", e.GetType().Name)),
                FontColorOverride = DangerColor,
                HorizontalExpand = true,
            });
            Logger.Error($"SkillWindow.UpdateState failed: {e}");
        }
    }

    private void ApplyState(SkillEuiState state)
    {
        _remainingPoints = state.RemainingPoints;
        _totalPoints = state.TotalPoints;

        _header.Text = state.JobName != null
            ? Loc.GetString("skills-window-header-job", ("job", state.JobName))
            : Loc.GetString("skills-window-header");

        _points.Text = Loc.GetString("skills-window-points-value",
            ("remaining", state.RemainingPoints),
            ("total", state.TotalPoints));

        if (!string.IsNullOrWhiteSpace(state.StatusMessage))
        {
            _status.Visible = true;
            _status.Text = state.StatusMessage;
        }
        else
        {
            _status.Visible = false;
        }

        _lockedHint.Visible = state.Locked;
        if (state.Locked)
            _lockedHint.Text = Loc.GetString("skills-window-locked-hint");

        _help.Visible = !state.Locked;
        _confirm.Disabled = state.Locked;
        _reset.Disabled = state.Locked;

        if (state.Locked || ConfirmDialogStale(state))
            CloseConfirmDialog();

        _skillsList.RemoveAllChildren();
        _levels.Clear();

        if (state.Skills.Count == 0)
        {
            _skillsList.AddChild(new Label
            {
                Text = Loc.GetString("skills-error-no-skillset"),
                FontColorOverride = WarningColor,
                HorizontalExpand = true,
                Margin = new Thickness(0, 24, 0, 0),
            });
            return;
        }

        string? lastCategory = null;
        foreach (var skill in state.Skills)
        {
            if (lastCategory != skill.Category)
            {
                lastCategory = skill.Category;
                _skillsList.AddChild(new Label
                {
                    Text = Loc.GetString("skills-category-" + skill.Category),
                    StyleClasses = { StyleClass.LabelHeading },
                    Margin = new Thickness(2, 10, 2, 2),
                    HorizontalExpand = true,
                });
            }

            _levels[skill.SkillId] = skill.CurrentLevel;
            _skillsList.AddChild(BuildSkillRow(skill));
        }
    }

    private Control BuildSkillRow(SkillUiEntry skill)
    {
        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            StyleClasses = { StyleClass.BackgroundPanelDark },
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 10,
            Margin = new Thickness(10, 8),
        };

        if (TryGetSkillIcon(skill.SkillId, out var texture) && texture != null)
        {
            row.AddChild(new TextureRect
            {
                Texture = texture,
                MinSize = new Vector2(40, 40),
                MaxSize = new Vector2(40, 40),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                VerticalAlignment = VAlignment.Top,
            });
        }

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 2,
        };

        body.AddChild(new Label
        {
            Text = skill.Name,
            StyleClasses = { StyleClass.LabelHeading },
            HorizontalExpand = true,
        });

        body.AddChild(new Label
        {
            Text = Loc.GetString("skills-window-level-line",
                ("level", Loc.GetString(SkillLevels.GetLevelNameKey(skill.CurrentLevel))),
                ("current", skill.CurrentLevel),
                ("max", skill.MaxLevel),
                ("bar", FormatLevelBar(skill.CurrentLevel, skill.MaxLevel))),
            FontColorOverride = MutedColor,
            HorizontalExpand = true,
        });

        if (!string.IsNullOrWhiteSpace(skill.Description))
        {
            body.AddChild(new Label
            {
                Text = skill.Description,
                FontColorOverride = MutedColor,
                HorizontalExpand = true,
            });
        }

        var affectsText = string.IsNullOrWhiteSpace(skill.AffectsHint)
            ? Loc.GetString("skills-affects-none")
            : skill.AffectsHint;
        var affectsInactive = string.Equals(affectsText, Loc.GetString("skills-affects-none"), StringComparison.Ordinal);

        body.AddChild(new Label
        {
            Text = Loc.GetString("skills-window-affects", ("text", affectsText)),
            FontColorOverride = affectsInactive ? MutedColor : GoodColor,
            HorizontalExpand = true,
        });

        if (!string.IsNullOrWhiteSpace(skill.EffectHint))
        {
            body.AddChild(new Label
            {
                Text = Loc.GetString("skills-window-effect", ("text", skill.EffectHint)),
                HorizontalExpand = true,
            });
        }

        if (skill.MinLevel > SkillLevels.Unskilled || skill.MaxLevel < SkillLevels.Max)
        {
            body.AddChild(new Label
            {
                Text = Loc.GetString("skills-window-role-range",
                    ("min", Loc.GetString(SkillLevels.GetLevelNameKey(skill.MinLevel))),
                    ("max", Loc.GetString(SkillLevels.GetLevelNameKey(skill.MaxLevel)))),
                FontColorOverride = MutedColor,
                HorizontalExpand = true,
            });
        }

        if (!string.IsNullOrWhiteSpace(skill.BlockReason))
        {
            body.AddChild(new Label
            {
                Text = skill.BlockReason,
                FontColorOverride = DangerColor,
                HorizontalExpand = true,
            });
        }

        var skillId = skill.SkillId;
        var stepper = BuildStepper(skill, skillId);

        row.AddChild(body);
        row.AddChild(stepper);
        panel.AddChild(row);
        return panel;
    }

    private Control BuildStepper(SkillUiEntry skill, string skillId)
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            VerticalAlignment = VAlignment.Center,
        };

        var decrease = new Button
        {
            Text = "−",
            MinWidth = 40,
            MinHeight = 40,
            Disabled = !skill.CanDecrease,
            ToolTip = Loc.GetString("skills-window-decrease"),
        };
        decrease.OnPressed += _ => OnDecrease?.Invoke(skillId);

        var value = new Label
        {
            Text = skill.NextLevelCost > 0
                ? Loc.GetString("skills-window-increase-cost", ("cost", skill.NextLevelCost))
                : "•",
            MinWidth = 44,
            Align = Label.AlignMode.Center,
            VerticalAlignment = VAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
        };

        var increase = new Button
        {
            Text = "+",
            MinWidth = 40,
            MinHeight = 40,
            Disabled = !skill.CanIncrease,
            ToolTip = Loc.GetString("skills-window-increase"),
        };
        increase.OnPressed += _ => OnIncrease?.Invoke(skillId);

        box.AddChild(decrease);
        box.AddChild(value);
        box.AddChild(increase);
        return box;
    }

    private void OpenConfirmDialog()
    {
        CloseConfirmDialog();
        var dialog = new SkillConfirmDialog();
        _confirmDialog = dialog;
        _confirmSnapshotRemaining = _remainingPoints;
        _confirmSnapshotTotal = _totalPoints;
        _confirmSnapshotLevels.Clear();
        foreach (var (id, level) in _levels)
            _confirmSnapshotLevels[id] = level;

        dialog.OnAccepted += () => OnConfirm?.Invoke();
        dialog.OnClose += () =>
        {
            if (ReferenceEquals(_confirmDialog, dialog))
                _confirmDialog = null;
            ClearConfirmSnapshot();
        };
        dialog.OpenFor(_remainingPoints, _totalPoints);
    }

    private void CloseConfirmDialog()
    {
        _confirmDialog?.Close();
        _confirmDialog = null;
        ClearConfirmSnapshot();
    }

    private void ClearConfirmSnapshot()
    {
        _confirmSnapshotLevels.Clear();
        _confirmSnapshotRemaining = -1;
        _confirmSnapshotTotal = -1;
    }

    /// <summary>
    /// Диалог держит снимок очков/уровней; если сервер уже сдвинул распределение — закрыть.
    /// </summary>
    private bool ConfirmDialogStale(SkillEuiState state)
    {
        if (_confirmDialog == null || !_confirmDialog.IsOpen)
            return false;

        if (state.RemainingPoints != _confirmSnapshotRemaining || state.TotalPoints != _confirmSnapshotTotal)
            return true;

        if (state.Skills.Count != _confirmSnapshotLevels.Count)
            return true;

        foreach (var skill in state.Skills)
        {
            if (!_confirmSnapshotLevels.TryGetValue(skill.SkillId, out var level) || level != skill.CurrentLevel)
                return true;
        }

        return false;
    }

    private bool TryGetSkillIcon(string skillId, out Texture? texture)
    {
        texture = null;
        try
        {
            if (!_prototypes.TryIndex<SkillPrototype>(skillId, out var proto) || proto.Icon is not { } icon)
                return false;

            texture = _sprite.Frame0(icon);
            return texture != null;
        }
        catch (Exception e)
        {
            Logger.Warning($"SkillWindow: icon failed for {skillId}: {e.Message}");
            return false;
        }
    }

    private static string FormatLevelBar(int current, int max)
    {
        var sb = new StringBuilder(max);
        for (var i = 1; i <= max; i++)
            sb.Append(i <= current ? '■' : '□');
        return sb.ToString();
    }
}
