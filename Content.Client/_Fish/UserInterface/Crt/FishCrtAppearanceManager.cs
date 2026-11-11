using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Client._Fish.UserInterface.Crt;

internal interface IFishCrtAppearanceManager
{
    FishCrtAppearanceSettings Settings { get; }

    event Action<FishCrtAppearanceSettings>? AppearanceChanged;
}

internal sealed partial class FishCrtAppearanceManager : IFishCrtAppearanceManager, IPostInjectInit
{
    [Dependency] private IConfigurationManager _configuration = default!;

    public FishCrtAppearanceSettings Settings { get; private set; } = new(true, true);

    public event Action<FishCrtAppearanceSettings>? AppearanceChanged;

    public void PostInject()
    {
        _configuration.OnValueChanged(FishCVars.FishCrtThemeEnabled, OnThemeEnabledChanged, true);
        _configuration.OnValueChanged(FishCVars.FishCrtEffectsEnabled, OnEffectsEnabledChanged, true);
    }

    private void OnThemeEnabledChanged(bool enabled)
    {
        UpdateSettings(new FishCrtAppearanceSettings(enabled, Settings.EffectsEnabled));
    }

    private void OnEffectsEnabledChanged(bool enabled)
    {
        UpdateSettings(new FishCrtAppearanceSettings(Settings.ThemeEnabled, enabled));
    }

    private void UpdateSettings(FishCrtAppearanceSettings settings)
    {
        if (Settings == settings)
            return;

        Settings = settings;
        AppearanceChanged?.Invoke(settings);
    }
}
