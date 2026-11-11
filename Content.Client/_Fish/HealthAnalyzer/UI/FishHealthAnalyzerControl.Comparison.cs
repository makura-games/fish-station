using Content.Client.HealthAnalyzer.UI;
using Content.Client.Stylesheets;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Fish.HealthAnalyzer.UI;

public sealed partial class FishHealthAnalyzerControl
{
    /* Сравнение последовательных активных замеров одного пациента и состояние разделов. */
    private EntityUid? _previousPatient;
    private bool _hasPreviousScan;
    private bool _canCompareDamage;
    private FixedPoint2 _previousTotalDamage;
    private readonly Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2> _previousDamageGroups = new();
    private readonly Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> _previousDamageTypes = new();

    private void InitializeSections()
    {
        InitializeSection(DamageHeading, "health-analyzer-window-damage-title");
        InitializeSection(TreatmentHeading, "health-analyzer-window-treatment-title");
        InitializeSection(ReagentsHeading, "health-analyzer-window-reagents-title");
    }

    private static void InitializeSection(CollapsibleHeading heading, string title)
    {
        heading.Label.AddStyleClass(HealthAnalyzerSheetlet.SectionTitle);
        heading.Label.ClipText = true;
        heading.Label.HorizontalExpand = true;
        heading.Title = Loc.GetString("health-analyzer-window-section-expanded", ("title", Loc.GetString(title)));
        heading.OnToggled += args => heading.Title = Loc.GetString(
            args.Pressed ? "health-analyzer-window-section-expanded" : "health-analyzer-window-section-collapsed",
            ("title", Loc.GetString(title)));
    }

    /// <summary>
    /// Resets the baseline when the patient changes and hides trends while readings are stale.
    /// </summary>
    private void BeginDamageComparison(EntityUid? patient, bool active)
    {
        if (_previousPatient != patient)
        {
            _previousPatient = patient;
            _hasPreviousScan = false;
            _previousDamageGroups.Clear();
            _previousDamageTypes.Clear();
        }

        _canCompareDamage = active && _hasPreviousScan;
        DamageTrendLabel.Visible = _canCompareDamage;
    }

    private void SaveDamageComparison(
        FixedPoint2 total,
        IReadOnlyDictionary<ProtoId<DamageGroupPrototype>, FixedPoint2> groups,
        IReadOnlyDictionary<ProtoId<DamageTypePrototype>, FixedPoint2> types)
    {
        // Не сохраняем устаревшие показания при потере связи и не удерживаем изменяемый словарь компонента.
        if (!IsScanActive)
            return;

        _previousTotalDamage = total;
        _previousDamageGroups.Clear();
        foreach (var (group, amount) in groups)
            _previousDamageGroups.Add(group, amount);
        _previousDamageTypes.Clear();
        foreach (var (type, amount) in types)
            _previousDamageTypes.Add(type, amount);
        _hasPreviousScan = true;
    }

    private void UpdateDamageTrend(Label label, FixedPoint2 delta)
    {
        label.Visible = _canCompareDamage;
        if (!_canCompareDamage)
            return;

        var key = delta < 0
            ? "health-analyzer-window-trend-improving"
            : delta > 0
                ? "health-analyzer-window-trend-worsening"
                : "health-analyzer-window-trend-unchanged";
        label.Text = Loc.GetString(key, ("amount", FixedPoint2.Abs(delta)));
        label.ToolTip = Loc.GetString("health-analyzer-window-trend-tooltip");
        var style = delta < 0 ? StyleClass.StatusGood : delta > 0 ? StyleClass.StatusBad : StyleClass.LabelWeak;
        label.RemoveStyleClass(StyleClass.StatusGood);
        label.RemoveStyleClass(StyleClass.StatusBad);
        label.RemoveStyleClass(StyleClass.LabelWeak);
        label.AddStyleClass(style);
        label.FontColorOverride = GetStatusColor(style);
    }

    private void AddDamageTrend(BoxContainer row, FixedPoint2 delta)
    {
        var label = new Label
        {
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VAlignment.Center,
            StyleClasses = { HealthAnalyzerSheetlet.DamageTrend },
        };
        UpdateDamageTrend(label, delta);
        row.AddChild(label);
    }
}
