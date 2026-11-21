using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Fish.Achievements.UI;
using Content.Client.Stylesheets;
using Content.Shared._Fish.Achievements;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client._Fish.Achievements;

/// <summary>
/// Журнал достижений: Nano DefaultWindow (без CRT).
/// </summary>
public sealed class AchievementWindow : DefaultWindow
{
    private static readonly Color GoodColor = StyleNano.GoodGreenFore;
    private static readonly Color MutedColor = StyleNano.DisabledFore;

    private readonly Label _summary;
    private readonly Label _percentLabel;
    private readonly ProgressBar _selectionProgress;
    private readonly BoxContainer _categoryRow;
    private readonly GridContainer _grid;
    private readonly FishAchievementDetailPane _detail;
    private readonly Dictionary<string, FishAchievementCard> _cards = new();
    private readonly Dictionary<Button, string?> _categoryButtons = new();

    private IPrototypeManager? _prototypes;
    private IReadOnlyDictionary<string, AchievementPlayerState>? _states;
    private string? _selectedCategory = AchievementCatalogStats.AllCategoriesId;
    private string? _selectedAchievementId;

    public AchievementWindow()
    {
        Title = Loc.GetString("fish-achievements-window-title");
        MinSize = new Vector2(920, 620);
        SetSize = new Vector2(1020, 700);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
            Margin = new Thickness(4),
        };

        var header = new PanelContainer
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

        var headerTop = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        _summary = new Label
        {
            StyleClasses = { StyleClass.LabelHeading },
            HorizontalExpand = true,
            Text = Loc.GetString("fish-achievements-summary", ("unlocked", 0), ("total", 0)),
        };

        _percentLabel = new Label
        {
            FontColorOverride = MutedColor,
            HorizontalAlignment = HAlignment.Right,
            Text = Loc.GetString("fish-achievements-progress-percent", ("percent", 0)),
        };

        headerTop.AddChild(_summary);
        headerTop.AddChild(_percentLabel);

        _selectionProgress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 0,
            MinHeight = 8,
            HorizontalExpand = true,
        };
        _selectionProgress.BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = StyleNano.PanelDark };
        _selectionProgress.ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = StyleNano.NanoGold };

        headerInner.AddChild(headerTop);
        headerInner.AddChild(_selectionProgress);
        header.AddChild(headerInner);
        root.AddChild(header);

        var categoryScroll = new ScrollContainer
        {
            HorizontalExpand = true,
            MinHeight = 44,
            HScrollEnabled = true,
            VScrollEnabled = false,
        };

        _categoryRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
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

        var catalogPanel = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            StyleClasses = { StyleClass.PanelDark },
            SizeFlagsStretchRatio = 1.35f,
        };

        var catalogScroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            Margin = new Thickness(10),
        };

        // Одна колонка: длинные RU-названия не режутся и не наезжают на прогресс.
        _grid = new GridContainer
        {
            Columns = 1,
            HSeparationOverride = 8,
            VSeparationOverride = 8,
            HorizontalExpand = true,
        };

        catalogScroll.AddChild(_grid);
        catalogPanel.AddChild(catalogScroll);

        _detail = new FishAchievementDetailPane
        {
            MinWidth = 320,
            SizeFlagsStretchRatio = 0.95f,
        };

        body.AddChild(catalogPanel);
        body.AddChild(_detail);
        root.AddChild(body);

        ContentsContainer.AddChild(root);
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

        UpdateSelectionProgress();
    }

    private void RebuildCategories()
    {
        _categoryRow.RemoveAllChildren();
        _categoryButtons.Clear();

        if (_prototypes == null)
            return;

        AddCategoryTab(AchievementCatalogStats.AllCategoriesId, Loc.GetString("fish-achievements-category-all"));

        foreach (var category in _prototypes.EnumeratePrototypes<AchievementCategoryPrototype>()
                     .OrderBy(c => c.Order)
                     .ThenBy(c => c.ID))
        {
            AddCategoryTab(category.ID, Loc.GetString(category.Name));
        }

        _selectedCategory ??= AchievementCatalogStats.AllCategoriesId;
        RefreshCategorySelection();
        UpdateSelectionProgress();
    }

    private void AddCategoryTab(string? categoryId, string text)
    {
        var button = new Button
        {
            Text = text,
            ToolTip = text,
            MinHeight = 36,
            StyleClasses = { StyleClass.ButtonOpenBoth },
        };

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
            button.Disabled = categoryId == _selectedCategory;
    }

    private void RebuildGrid()
    {
        _grid.RemoveAllChildren();
        _cards.Clear();

        if (_prototypes == null || _states == null)
        {
            _detail.ShowPlaceholder();
            UpdateSelectionProgress();
            return;
        }

        var list = AchievementCatalogStats
            .EnumerateVisible(_prototypes, _states, _selectedCategory)
            .OrderBy(a => a.Order)
            .ThenBy(a => a.ID)
            .ToList();

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

        UpdateSelectionProgress();
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

    private void UpdateSelectionProgress()
    {
        if (_prototypes == null || _states == null)
            return;

        var progress = AchievementCatalogStats.GetSelectionProgress(_prototypes, _states, _selectedCategory);
        var name = Loc.GetString(progress.DisplayName);

        _summary.Text = Loc.GetString(
            "fish-achievements-category-progress",
            ("name", name),
            ("unlocked", progress.Unlocked),
            ("total", progress.Total),
            ("percent", progress.Percent));
        _summary.FontColorOverride = progress.Unlocked > 0 ? GoodColor : null;
        _percentLabel.Text = Loc.GetString("fish-achievements-progress-percent", ("percent", progress.Percent));
        _selectionProgress.Value = progress.Percent;
    }
}
