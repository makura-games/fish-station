using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Palette;
using Content.Shared.Mobs;
using Robust.Client.Animations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Animations;
using Robust.Shared.Timing;

// Общий namespace связывает эту часть с исходным окном анализатора.
namespace Content.Client.HealthAnalyzer.UI;

public sealed partial class HealthAnalyzerWindow
{
    /* Оформление Fish: палитра состояния пациента и локальные анимации окна. */
    private const string FadeInAnimationKey = "health-analyzer-fade-in";
    private const float FadeInDuration = 0.16f;
    private const float ThemeTransitionDuration = 0.3f;

    private HealthAnalyzerTheme _theme = HealthAnalyzerTheme.Active;
    private ColorPalette _displayPalette = HealthAnalyzerSheetlet.GetPalette(HealthAnalyzerTheme.Active);
    private ColorPalette _startPalette = HealthAnalyzerSheetlet.GetPalette(HealthAnalyzerTheme.Active);
    private ColorPalette _targetPalette = HealthAnalyzerSheetlet.GetPalette(HealthAnalyzerTheme.Active);
    private float _themeElapsed = ThemeTransitionDuration;

    private static readonly Animation FadeInAnimation = new()
    {
        Length = TimeSpan.FromSeconds(FadeInDuration),
        AnimationTracks =
        {
            new AnimationTrackControlProperty
            {
                Property = nameof(Modulate),
                InterpolationMode = AnimationInterpolationMode.Cubic,
                KeyFrames =
                {
                    new AnimationTrackProperty.KeyFrame(Color.White.WithAlpha(0f), 0f),
                    new AnimationTrackProperty.KeyFrame(Color.White, FadeInDuration),
                },
            },
        },
    };

    private void InitializeFishInterface()
    {
        LayoutContainer.SetAnchorPreset(AnalyzerScroll, LayoutContainer.LayoutPreset.Wide);
        LayoutContainer.SetAnchorPreset(EffectsLayer, LayoutContainer.LayoutPreset.Wide);
    }

    private void UpdateFishInterface()
    {
        SetTheme(HealthAnalyzer.IsScanActive, HealthAnalyzer.PatientState);
        EffectsLayer.SetScanActive(HealthAnalyzer.IsScanActive);
        if (HealthAnalyzer.IsScanActive)
            EffectsLayer.TriggerScan();
    }

    private void SetTheme(bool scanActive, MobState patientState)
    {
        var theme = !scanActive
            ? HealthAnalyzerTheme.Inactive
            : patientState switch
            {
                MobState.Critical => HealthAnalyzerTheme.Critical,
                MobState.Dead => HealthAnalyzerTheme.Dead,
                _ => HealthAnalyzerTheme.Active,
            };

        if (_theme != theme)
        {
            _theme = theme;
            // При смене состояния продолжаем переход от уже показанных цветов.
            _startPalette = _displayPalette;
            _targetPalette = HealthAnalyzerSheetlet.GetPalette(theme);
            _themeElapsed = 0f;
        }

        ApplyPalette(this, _displayPalette, theme == HealthAnalyzerTheme.Inactive);
        EffectsLayer.SetAccentColor(_displayPalette.Base);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_themeElapsed >= ThemeTransitionDuration)
            return;

        _themeElapsed = Math.Min(ThemeTransitionDuration, _themeElapsed + args.DeltaSeconds);
        var progress = _themeElapsed / ThemeTransitionDuration;
        var easedProgress = progress * progress * (3f - 2f * progress);
        _displayPalette = InterpolatePalette(_startPalette, _targetPalette, easedProgress);
        ApplyPalette(this, _displayPalette, _theme == HealthAnalyzerTheme.Inactive);
        EffectsLayer.SetAccentColor(_displayPalette.Base);
    }

    private static void ApplyPalette(Control control, ColorPalette palette, bool inactive)
    {
        if (control is PanelContainer panel &&
            HealthAnalyzerSheetlet.CreateThemedPanel(panel, palette) is { } style)
        {
            panel.PanelOverride = style;
        }

        // Рабочий сканер сохраняет сигнальные цвета показателей; отключённый становится серым целиком.
        if (control is Label label && (inactive || !HasSemanticStatus(label)))
        {
            label.FontColorOverride = label.HasStyleClass(HealthAnalyzerSheetlet.MetricKey) ||
                                      label.HasStyleClass(HealthAnalyzerSheetlet.DamageType) ||
                                      label.HasStyleClass(StyleClass.LabelSubText)
                ? palette.TextDark
                : palette.Text;
        }

        foreach (var child in control.Children)
            ApplyPalette(child, palette, inactive);
    }

    private static bool HasSemanticStatus(Label label)
    {
        return label.HasStyleClass(StyleClass.StatusGood) ||
               label.HasStyleClass(StyleClass.StatusOkay) ||
               label.HasStyleClass(StyleClass.StatusWarning) ||
               label.HasStyleClass(StyleClass.StatusBad) ||
               label.HasStyleClass(StyleClass.StatusCritical);
    }

    private static ColorPalette InterpolatePalette(ColorPalette source, ColorPalette target, float amount)
    {
        return source with
        {
            Base = Color.InterpolateBetween(source.Base, target.Base, amount),
            Element = Color.InterpolateBetween(source.Element, target.Element, amount),
            Background = Color.InterpolateBetween(source.Background, target.Background, amount),
            BackgroundLight = Color.InterpolateBetween(source.BackgroundLight, target.BackgroundLight, amount),
            BackgroundDark = Color.InterpolateBetween(source.BackgroundDark, target.BackgroundDark, amount),
            Text = Color.InterpolateBetween(source.Text, target.Text, amount),
            TextDark = Color.InterpolateBetween(source.TextDark, target.TextDark, amount),
        };
    }

    protected override void Opened()
    {
        base.Opened();

        if (HasRunningAnimation(FadeInAnimationKey))
            StopAnimation(FadeInAnimationKey);

        Modulate = Color.White.WithAlpha(0f);
        PlayAnimation(FadeInAnimation, FadeInAnimationKey);
    }
}
