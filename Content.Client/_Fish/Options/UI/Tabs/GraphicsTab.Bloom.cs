using Content.Client._Fish.Graphics.Bloom;
using Content.Client.Options.UI;
using Content.Shared._Fish.Graphics;

namespace Content.Client.Options.UI.Tabs;

public sealed partial class GraphicsTab
{
    // Настройки свечения Fish, их предпросмотр и возврат к сохранённым значениям.
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private ILocalizationManager _loc = default!;

    private void InitializeFishBloomOptions()
    {
        var enabled = Control.AddOptionCheckBox(FishBloomCVars.Enabled, FishBloomEnabledCheckBox);
        var strength = Control.AddOptionPercentSlider(FishBloomCVars.Strength, FishBloomStrengthSlider);
        var quality = Control.AddOptionDropDown(
            FishBloomCVars.Quality,
            FishBloomQualityDropDown,
            [
                new OptionDropDownCVar<string>.ValueOption(
                    FishBloomCVars.QualityLow,
                    _loc.GetString("fish-options-bloom-quality-low")),
                new OptionDropDownCVar<string>.ValueOption(
                    FishBloomCVars.QualityMedium,
                    _loc.GetString("fish-options-bloom-quality-medium")),
                new OptionDropDownCVar<string>.ValueOption(
                    FishBloomCVars.QualityHigh,
                    _loc.GetString("fish-options-bloom-quality-high")),
            ]);

        FishBloomStrengthSlider.Slider.Rounded = true;
        FishBloomStrengthSlider.Slider.RoundingDecimals = 2;

        enabled.ImmediateValueChanged += PreviewFishBloomEnabled;
        strength.ImmediateValueChanged += PreviewFishBloomStrength;
        quality.ImmediateValueChanged += PreviewFishBloomQuality;
        Control.ValuesReset += RefreshFishBloomOptions;
    }

    private void RefreshFishBloomOptions()
    {
        if (!float.IsFinite(FishBloomStrengthSlider.Slider.Value))
            FishBloomStrengthSlider.Slider.Value = 0f;

        // Стандартный список выбирает первый пункт для неизвестного CVar, но рендер использует Medium.
        if (_cfg.GetCVar(FishBloomCVars.Quality) is not
            (FishBloomCVars.QualityLow or FishBloomCVars.QualityMedium or FishBloomCVars.QualityHigh))
        {
            var button = FishBloomQualityDropDown.Button;
            for (var index = 0; index < button.ItemCount; index++)
            {
                if (button.GetItemMetadata(index) is not FishBloomCVars.QualityMedium)
                    continue;

                button.SelectId(button.GetItemId(index));
                break;
            }
        }

        PreviewFishBloomEnabled(FishBloomEnabledCheckBox.Pressed);
        PreviewFishBloomStrength(FishBloomStrengthSlider.Slider.Value);
        if (FishBloomQualityDropDown.Button.SelectedMetadata is string quality)
            PreviewFishBloomQuality(quality);
    }

    private void PreviewFishBloomEnabled(bool enabled)
    {
        FishBloomStrengthSlider.Visible = enabled;
        FishBloomQualityDropDown.Visible = enabled;

        if (_entity.TrySystem<FishBloomSystem>(out var bloom))
            bloom.PreviewEnabled(enabled);
    }

    private void PreviewFishBloomStrength(float strength)
    {
        if (_entity.TrySystem<FishBloomSystem>(out var bloom))
            bloom.PreviewStrength(strength);
    }

    private void PreviewFishBloomQuality(string quality)
    {
        if (_entity.TrySystem<FishBloomSystem>(out var bloom))
            bloom.PreviewQuality(quality);
    }

    protected override void ExitedTree()
    {
        // Окно настроек переиспользуется, поэтому откатываем предпросмотр при закрытии, а не Dispose.
        if (_entity.TrySystem<FishBloomSystem>(out var bloom))
        {
            bloom.PreviewEnabled(_cfg.GetCVar(FishBloomCVars.Enabled));
            bloom.PreviewStrength(_cfg.GetCVar(FishBloomCVars.Strength));
            bloom.PreviewQuality(_cfg.GetCVar(FishBloomCVars.Quality));
        }

        base.ExitedTree();
    }
}
