using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Fish.Achievements.UI;
using Content.Client._Fish.UserInterface.Crt;
using Content.Client.UserInterface.Controls;
using Content.Shared._Fish.Achievements;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Fish.Achievements;

/// <summary>
/// Окно достижений: станционный журнал с вкладками-иконками, сеткой карточек и панелью деталей.
/// </summary>
public sealed class AchievementWindow : FancyWindow
{
    private readonly FishCrtLabel _summary;
    private readonly FishCrtLabel _percentLabel;
    private readonly ProgressBar _globalProgress;
    private readonly BoxContainer _categoryRow;
    private readonly GridContainer _grid;
    private readonly FishAchievementDetailPane _detail;
    private readonly Dictionary<string, FishAchievementCard> _cards = new();
    private readonly Dictionary<FishCrtActionButton, string?> _categoryButtons = new();

    private IPrototypeManager? _prototypes;
    private IReadOnlyDictionary<string, AchievementPlayerState>? _states;
    private string? _selectedCategory;
    private string? _selectedAchievementId;

    public AchievementWindow()
    {
        Title = Loc.GetString("fish-achievements-window-title");
        MinSize = new Vector2(880, 580);
        SetSize = new Vector2(960, 640);

        var theme = new FishCrtThemeScope
        {
            Palette = FishCrtPalettePreset.Station,
            Effects = FishCrtEffects.None,
            HorizontalExpand = true,
            VerticalExpand = true,
            BorderThickness = 0,
            BackgroundOpacity = 0,
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 10,
            Margin = new Thickness(12, 8, 12, 12),
        };

        var header = new FishCrtPanel
        {
            Variant = FishCrtPanelVariant.Surface,
            Rounded = true,
            Effects = FishCrtEffects.None,
            BackgroundOpacity = 0.65f,
            BorderThickness = 0,
            HorizontalExpand = true,
        };

        var headerInner = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 6,
            Margin = new Thickness(14, 10),
        };

        var headerTop = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        _summary = new FishCrtLabel
        {
            Heading = true,
            TextFontSize = 15,
            HorizontalExpand = true,
            Text = Loc.GetString("fish-achievements-summary", ("unlocked", 0), ("total", 0)),
        };

        _percentLabel = new FishCrtLabel
        {
            TextFontSize = 13,
            Tone = FishCrtTone.Muted,
            HorizontalAlignment = HAlignment.Right,
            Text = Loc.GetString("fish-achievements-progress-percent", ("percent", 0)),
        };

        headerTop.AddChild(_summary);
        headerTop.AddChild(_percentLabel);

        _globalProgress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 0,
            MinHeight = 8,
            HorizontalExpand = true,
        };

        headerInner.AddChild(headerTop);
        headerInner.AddChild(_globalProgress);
        header.AddChild(headerInner);
        root.AddChild(header);

        var categoryScroll = new ScrollContainer
        {
            HorizontalExpand = true,
            MinHeight = 52,
            HScrollEnabled = true,
            VScrollEnabled = false,
        };

        _categoryRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
        };
        categoryScroll.AddChild(_categoryRow);
        root.AddChild(categoryScroll);

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 10,
        };

        var catalogPanel = new FishCrtPanel
        {
            Variant = FishCrtPanelVariant.Inset,
            Rounded = true,
            Effects = FishCrtEffects.None,
            BackgroundOpacity = 0.48f,
            BorderThickness = 0,
            HorizontalExpand = true,
            VerticalExpand = true,
            SizeFlagsStretchRatio = 1.35f,
        };

        var catalogScroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            Margin = new Thickness(10),
        };

        _grid = new GridContainer
        {
            Columns = 2,
            HSeparationOverride = 8,
            VSeparationOverride = 8,
            HorizontalExpand = true,
        };

        catalogScroll.AddChild(_grid);
        catalogPanel.AddChild(catalogScroll);

        _detail = new FishAchievementDetailPane
        {
            MinWidth = 300,
            SizeFlagsStretchRatio = 1f,
        };

        body.AddChild(catalogPanel);
        body.AddChild(_detail);
        root.AddChild(body);

        theme.AddChild(root);
        ContentsContainer.AddChild(theme);
    }

    public void Populate(
        IPrototypeManager prototypes,
        IReadOnlyDictionary<string, AchievementPlayerState> states)
    {
        _prototypes = prototypes;
        _states = states;

        RebuildCategories();
        RebuildGrid();
    }

    public void UpdateEntry(AchievementPlayerState state)
    {
        if (_cards.TryGetValue(state.AchievementId, out var card) &&
            _prototypes != null &&
            _prototypes.TryIndex<AchievementPrototype>(state.AchievementId, out var proto))
        {
            card.Bind(proto, state);
        }

        if (_selectedAchievementId == state.AchievementId &&
            _prototypes != null &&
            _prototypes.TryIndex<AchievementPrototype>(state.AchievementId, out var selected))
        {
            _detail.Bind(selected, state);
        }

        UpdateSummary();
    }

    private void RebuildCategories()
    {
        _categoryRow.RemoveAllChildren();
        _categoryButtons.Clear();

        if (_prototypes == null)
            return;

        AddCategoryTab(null, Loc.GetString("fish-achievements-category-all"), FishCrtIcons.Home);

        string? firstCategory = null;
        foreach (var category in _prototypes.EnumeratePrototypes<AchievementCategoryPrototype>()
                     .OrderBy(c => c.Order)
                     .ThenBy(c => c.ID))
        {
            firstCategory ??= category.ID;
            var icon = string.IsNullOrWhiteSpace(category.Icon) ? FishCrtIcons.Medal : category.Icon!;
            AddCategoryTab(category.ID, Loc.GetString(category.Name), icon);
        }

        if (_selectedCategory == null && firstCategory != null)
            _selectedCategory = firstCategory;

        RefreshCategorySelection();
    }

    private void AddCategoryTab(string? categoryId, string tooltip, string iconState)
    {
        var button = new FishCrtActionButton
        {
            IconState = iconState,
            ToolTip = tooltip,
            Variant = categoryId == _selectedCategory ? FishCrtButtonVariant.Filled : FishCrtButtonVariant.Outline,
            Selected = categoryId == _selectedCategory,
            MinHeight = 44,
            MinWidth = 44,
            ContentMargin = new Thickness(10, 8),
        };
        button.Background.Rounded = true;
        button.Background.BorderThickness = categoryId == _selectedCategory ? 0 : 1;
        button.Background.Effects = FishCrtEffects.None;

        button.OnPressed += _ =>
        {
            _selectedCategory = categoryId;
            _selectedAchievementId = null;
            RefreshCategorySelection();
            RebuildGrid();
        };

        _categoryButtons[button] = categoryId;
        _categoryRow.AddChild(button);
    }

    private void RefreshCategorySelection()
    {
        foreach (var (button, categoryId) in _categoryButtons)
        {
            var selected = categoryId == _selectedCategory;
            button.Selected = selected;
            button.Variant = selected ? FishCrtButtonVariant.Filled : FishCrtButtonVariant.Outline;
            button.Background.BorderThickness = selected ? 0 : 1;
        }
    }

    private void RebuildGrid()
    {
        _grid.RemoveAllChildren();
        _cards.Clear();

        if (_prototypes == null || _states == null)
        {
            _detail.ShowPlaceholder();
            UpdateSummary();
            return;
        }

        IEnumerable<AchievementPrototype> achievements = _prototypes
            .EnumeratePrototypes<AchievementPrototype>()
            .Where(a => _selectedCategory == null || a.Category == _selectedCategory);

        if (_selectedCategory == null)
        {
            achievements = achievements.Where(a =>
            {
                if (a.Condition != AchievementConditionKeys.Manual)
                    return true;

                return _states.TryGetValue(a.ID, out var st) && (st.Unlocked || st.Progress > 0);
            });
        }

        var list = achievements.OrderBy(a => a.Order).ThenBy(a => a.ID).ToList();
        FishAchievementCard? firstCard = null;

        foreach (var proto in list)
        {
            _states.TryGetValue(proto.ID, out var state);
            var card = new FishAchievementCard();
            card.Bind(proto, state);
            card.SetSelected(proto.ID == _selectedAchievementId);
            card.OnPressed += _ => SelectAchievement(proto.ID);

            _cards[proto.ID] = card;
            _grid.AddChild(card);
            firstCard ??= card;
        }

        if (_selectedAchievementId != null &&
            _cards.ContainsKey(_selectedAchievementId) &&
            _prototypes.TryIndex<AchievementPrototype>(_selectedAchievementId, out var selectedProto))
        {
            _states.TryGetValue(_selectedAchievementId, out var selectedState);
            _detail.Bind(selectedProto, selectedState);
            _cards[_selectedAchievementId].SetSelected(true);
        }
        else if (firstCard != null)
        {
            SelectAchievement(firstCard.AchievementId);
        }
        else
        {
            _selectedAchievementId = null;
            _detail.ShowPlaceholder();
        }

        UpdateSummary();
    }

    private void SelectAchievement(string achievementId)
    {
        if (_selectedAchievementId == achievementId)
            return;

        if (_selectedAchievementId != null && _cards.TryGetValue(_selectedAchievementId, out var prev))
            prev.SetSelected(false);

        _selectedAchievementId = achievementId;

        if (_cards.TryGetValue(achievementId, out var card))
            card.SetSelected(true);

        if (_prototypes != null &&
            _states != null &&
            _prototypes.TryIndex<AchievementPrototype>(achievementId, out var proto))
        {
            _states.TryGetValue(achievementId, out var state);
            _detail.Bind(proto, state);
        }
    }

    private void UpdateSummary()
    {
        if (_prototypes == null || _states == null)
            return;

        var total = _prototypes.EnumeratePrototypes<AchievementPrototype>().Count();
        var unlocked = _states.Values.Count(s => s.Unlocked);
        var percent = total > 0 ? (int) System.Math.Round(unlocked * 100d / total) : 0;

        _summary.Text = Loc.GetString("fish-achievements-summary", ("unlocked", unlocked), ("total", total));
        _summary.Tone = unlocked > 0 ? FishCrtTone.Good : FishCrtTone.Default;
        _percentLabel.Text = Loc.GetString("fish-achievements-progress-percent", ("percent", percent));
        _globalProgress.Value = percent;

        var palette = FishCrtThemeHelpers.FindContext(_globalProgress).Palette;
        _globalProgress.BackgroundStyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = palette.Background.WithAlpha(0.95f),
        };
        _globalProgress.ForegroundStyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = palette.Border,
        };
    }
}
