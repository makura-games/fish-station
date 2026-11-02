using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Fish.Graphics.Bloom;
using Content.Client._Sunrise.Shaders.Bloom;
using Content.Client.Options.UI;
using Content.Client.Options.UI.Tabs;
using Content.IntegrationTests.Pair;
using Content.Shared._Fish.Graphics;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Fish.Graphics;

[TestFixture]
public sealed class FishBloomOptionsTest
{
    /// <summary>
    /// Graphics controls preview changes, while apply, reset, defaults and close preserve their saving contract.
    /// </summary>
    [TestCase("reset")]
    [TestCase("defaults")]
    [TestCase("close")]
    [TestCase("apply")]
    [TestCase("zero")]
    public async Task GraphicsOptionsPreviewAndSaveLifecycle(string action)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var cfg = client.CfgMan;
        var originalEnabled = cfg.GetCVar(FishBloomCVars.Enabled);
        var originalStrength = cfg.GetCVar(FishBloomCVars.Strength);
        var originalQuality = cfg.GetCVar(FishBloomCVars.Quality);
        DefaultWindow window = null;
        GraphicsTab tab = null;
        CheckBox enabled = null;
        OptionSlider strength = null;
        OptionDropDown quality = null;
        IOverlayManager overlays = null;

        try
        {
            await client.WaitPost(() =>
            {
                cfg.SetCVar(FishBloomCVars.Enabled, false);
                cfg.SetCVar(FishBloomCVars.Strength, 0.2f);
                cfg.SetCVar(FishBloomCVars.Quality, FishBloomCVars.QualityHigh);
                overlays = client.ResolveDependency<IOverlayManager>();
                window = client.ResolveDependency<IUserInterfaceManager>().CreateWindow<DefaultWindow>();
                tab = new GraphicsTab();
                window.Contents.AddChild(tab);
                window.MinSize = new Vector2(800, 600);
                window.OpenCentered();
                enabled = tab.FindControl<CheckBox>("FishBloomEnabledCheckBox");
                strength = tab.FindControl<OptionSlider>("FishBloomStrengthSlider");
                quality = tab.FindControl<OptionDropDown>("FishBloomQualityDropDown");
            });
            await pair.RunTicksSync(1);
            await client.WaitAssertion(() =>
            {
                Assert.That(enabled.Pressed, Is.False);
                Assert.That(strength.Visible, Is.False);
                Assert.That(quality.Visible, Is.False);
                AssertBloom(overlays, false, 0.2f, FishBloomQuality.High);
            });

            await Click(pair, enabled);
            await client.WaitPost(() => strength.Slider.Value = 0.8f);
            await Click(pair, quality.Button);
            Button lowOption = null;
            await client.WaitPost(() =>
            {
                var lowText = client.ResolveDependency<ILocalizationManager>()
                    .GetString("fish-options-bloom-quality-low");
                lowOption = EnumerateButtons(quality.Button.OptionsScroll).Single(button => button.Text == lowText);
            });
            await Click(pair, lowOption);
            await client.WaitAssertion(() =>
            {
                AssertBloom(overlays, true, 0.8f, FishBloomQuality.Low);
                Assert.That(strength.Visible, Is.True);
                Assert.That(quality.Visible, Is.True);
                Assert.That(cfg.GetCVar(FishBloomCVars.Enabled), Is.False);
                Assert.That(cfg.GetCVar(FishBloomCVars.Strength), Is.EqualTo(0.2f));
                Assert.That(cfg.GetCVar(FishBloomCVars.Quality), Is.EqualTo(FishBloomCVars.QualityHigh));
            });

            if (action == "zero")
            {
                await client.WaitPost(() => strength.Slider.Value = 0f);
                await client.WaitAssertion(() =>
                {
                    AssertBloom(overlays, true, 0f, FishBloomQuality.Low);
                    Assert.That(quality.Button.SelectedMetadata, Is.EqualTo(FishBloomCVars.QualityLow));
                    Assert.That(cfg.GetCVar(FishBloomCVars.Strength), Is.EqualTo(0.2f));
                });
                await client.WaitPost(() => strength.Slider.Value = 0.6f);
                await client.WaitAssertion(() =>
                {
                    AssertBloom(overlays, true, 0.6f, FishBloomQuality.Low);
                    Assert.That(cfg.GetCVar(FishBloomCVars.Enabled), Is.False);
                    Assert.That(cfg.GetCVar(FishBloomCVars.Strength), Is.EqualTo(0.2f));
                    Assert.That(cfg.GetCVar(FishBloomCVars.Quality), Is.EqualTo(FishBloomCVars.QualityHigh));
                });
            }

            if (action is "close" or "zero")
            {
                await client.WaitPost(window.Close);
            }
            else
            {
                var buttonName = action switch
                {
                    "reset" => "ResetButton",
                    "defaults" => "DefaultButton",
                    "apply" => "ApplyButton",
                    _ => throw new ArgumentOutOfRangeException(nameof(action)),
                };
                await Click(pair, tab.Control.FindControl<Button>(buttonName));
            }

            await client.WaitAssertion(() =>
            {
                if (action == "defaults")
                {
                    AssertBloom(overlays, true, FishBloomCVars.Strength.DefaultValue, FishBloomQuality.Medium);
                    Assert.That(enabled.Pressed, Is.True);
                    Assert.That(strength.Visible, Is.True);
                    Assert.That(quality.Button.SelectedMetadata, Is.EqualTo(FishBloomCVars.QualityMedium));
                }
                else if (action == "apply")
                {
                    AssertBloom(overlays, true, 0.8f, FishBloomQuality.Low);
                }
                else
                {
                    AssertBloom(overlays, false, 0.2f, FishBloomQuality.High);
                    if (action == "reset")
                    {
                        Assert.That(enabled.Pressed, Is.False);
                        Assert.That(strength.Visible, Is.False);
                        Assert.That(quality.Button.SelectedMetadata, Is.EqualTo(FishBloomCVars.QualityHigh));
                    }
                }

                Assert.That(cfg.GetCVar(FishBloomCVars.Enabled), Is.EqualTo(action == "apply"));
                Assert.That(cfg.GetCVar(FishBloomCVars.Strength), Is.EqualTo(action == "apply" ? 0.8f : 0.2f));
                Assert.That(cfg.GetCVar(FishBloomCVars.Quality),
                    Is.EqualTo(action == "apply" ? FishBloomCVars.QualityLow : FishBloomCVars.QualityHigh));
            });

            // Close переиспользуемого окна обязан отменять предпросмотр без его уничтожения.
            await client.WaitPost(window.Close);
            await client.WaitAssertion(() =>
            {
                Assert.That(window.Disposed, Is.False);
                AssertBloom(overlays, action == "apply", action == "apply" ? 0.8f : 0.2f,
                    action == "apply" ? FishBloomQuality.Low : FishBloomQuality.High);
            });

            await client.WaitPost(() =>
            {
                tab.Control.ReloadValues();
                window.OpenCentered();
            });
            await client.WaitAssertion(() =>
            {
                Assert.That(enabled.Pressed, Is.EqualTo(action == "apply"));
                Assert.That(strength.Slider.Value, Is.EqualTo(action == "apply" ? 0.8f : 0.2f));
                Assert.That(quality.Button.SelectedMetadata,
                    Is.EqualTo(action == "apply" ? FishBloomCVars.QualityLow : FishBloomCVars.QualityHigh));
            });
        }
        finally
        {
            await client.WaitPost(() =>
            {
                window?.Close();
                cfg.SetCVar(FishBloomCVars.Enabled, originalEnabled);
                cfg.SetCVar(FishBloomCVars.Strength, originalStrength);
                cfg.SetCVar(FishBloomCVars.Quality, originalQuality);
            });
        }

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Unknown saved quality uses the renderer's fallback without rewriting configuration until Apply is pressed.
    /// </summary>
    [Test]
    public async Task UnknownQualityUsesMediumWithoutSaving()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var cfg = client.CfgMan;
        var originalEnabled = cfg.GetCVar(FishBloomCVars.Enabled);
        var originalStrength = cfg.GetCVar(FishBloomCVars.Strength);
        var originalQuality = cfg.GetCVar(FishBloomCVars.Quality);
        const string unknownQuality = "UnknownQuality";
        DefaultWindow window = null;
        GraphicsTab tab = null;
        IOverlayManager overlays = null;

        try
        {
            await client.WaitPost(() =>
            {
                cfg.SetCVar(FishBloomCVars.Enabled, true);
                cfg.SetCVar(FishBloomCVars.Strength, 0.4f);
                cfg.SetCVar(FishBloomCVars.Quality, unknownQuality);
                overlays = client.ResolveDependency<IOverlayManager>();
                window = client.ResolveDependency<IUserInterfaceManager>().CreateWindow<DefaultWindow>();
                tab = new GraphicsTab();
                window.Contents.AddChild(tab);
                window.MinSize = new Vector2(800, 600);
                window.OpenCentered();
            });
            await pair.RunTicksSync(1);
            await client.WaitAssertion(() =>
            {
                AssertBloom(overlays, true, 0.4f, FishBloomQuality.Medium);
                Assert.That(tab.FindControl<OptionDropDown>("FishBloomQualityDropDown").Button.SelectedMetadata,
                    Is.EqualTo(FishBloomCVars.QualityMedium));
                Assert.That(cfg.GetCVar(FishBloomCVars.Quality), Is.EqualTo(unknownQuality));
            });
            await client.WaitPost(() => tab.FindControl<OptionSlider>("FishBloomStrengthSlider").Slider.Value = 0.8f);
            await client.WaitPost(window.Close);
            await client.WaitAssertion(() =>
            {
                AssertBloom(overlays, true, 0.4f, FishBloomQuality.Medium);
                Assert.That(cfg.GetCVar(FishBloomCVars.Quality), Is.EqualTo(unknownQuality));
            });
            await client.WaitPost(() =>
            {
                tab.Control.ReloadValues();
                window.OpenCentered();
            });
            await pair.RunTicksSync(1);
            await Click(pair, tab.Control.FindControl<Button>("ApplyButton"));
            await client.WaitAssertion(() =>
            {
                AssertBloom(overlays, true, 0.4f, FishBloomQuality.Medium);
                Assert.That(cfg.GetCVar(FishBloomCVars.Quality), Is.EqualTo(FishBloomCVars.QualityMedium));
            });
        }
        finally
        {
            await client.WaitPost(() =>
            {
                window?.Close();
                cfg.SetCVar(FishBloomCVars.Enabled, originalEnabled);
                cfg.SetCVar(FishBloomCVars.Strength, originalStrength);
                cfg.SetCVar(FishBloomCVars.Quality, originalQuality);
            });
        }

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Nonfinite saved intensity has the same safe value in the UI and renderer without changing saved configuration.
    /// </summary>
    [TestCase(float.NaN, 0f)]
    [TestCase(float.PositiveInfinity, 1f)]
    [TestCase(float.NegativeInfinity, 0f)]
    public async Task NonfiniteStrengthPreviewAndDefaults(float savedStrength, float expectedStrength)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var cfg = client.CfgMan;
        var originalEnabled = cfg.GetCVar(FishBloomCVars.Enabled);
        var originalStrength = cfg.GetCVar(FishBloomCVars.Strength);
        var originalQuality = cfg.GetCVar(FishBloomCVars.Quality);
        DefaultWindow window = null;
        GraphicsTab tab = null;
        IOverlayManager overlays = null;

        try
        {
            await client.WaitPost(() =>
            {
                cfg.SetCVar(FishBloomCVars.Enabled, true);
                cfg.SetCVar(FishBloomCVars.Strength, savedStrength);
                cfg.SetCVar(FishBloomCVars.Quality, FishBloomCVars.QualityHigh);
                overlays = client.ResolveDependency<IOverlayManager>();
            });
            await client.WaitAssertion(() => AssertBloom(overlays, true, expectedStrength, FishBloomQuality.High));
            await client.WaitPost(() =>
            {
                window = client.ResolveDependency<IUserInterfaceManager>().CreateWindow<DefaultWindow>();
                tab = new GraphicsTab();
                window.Contents.AddChild(tab);
                window.MinSize = new Vector2(800, 600);
                window.OpenCentered();
            });
            await pair.RunTicksSync(1);
            await client.WaitAssertion(() =>
            {
                Assert.That(tab.FindControl<OptionSlider>("FishBloomStrengthSlider").Slider.Value,
                    Is.EqualTo(expectedStrength));
                AssertBloom(overlays, true, expectedStrength, FishBloomQuality.High);
                Assert.That(cfg.GetCVar(FishBloomCVars.Strength), Is.EqualTo(savedStrength));
            });

            await Click(pair, tab.Control.FindControl<Button>("DefaultButton"));
            await client.WaitAssertion(() =>
            {
                Assert.That(tab.FindControl<OptionSlider>("FishBloomStrengthSlider").Slider.Value,
                    Is.EqualTo(FishBloomCVars.Strength.DefaultValue));
                AssertBloom(overlays, true, FishBloomCVars.Strength.DefaultValue, FishBloomQuality.Medium);
                Assert.That(cfg.GetCVar(FishBloomCVars.Strength), Is.EqualTo(savedStrength));
            });
            await client.WaitPost(window.Close);
            await client.WaitAssertion(() =>
            {
                AssertBloom(overlays, true, expectedStrength, FishBloomQuality.High);
                Assert.That(cfg.GetCVar(FishBloomCVars.Strength), Is.EqualTo(savedStrength));
            });
        }
        finally
        {
            await client.WaitPost(() =>
            {
                window?.Close();
                cfg.SetCVar(FishBloomCVars.Enabled, originalEnabled);
                cfg.SetCVar(FishBloomCVars.Strength, originalStrength);
                cfg.SetCVar(FishBloomCVars.Quality, originalQuality);
            });
        }

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The legacy bloom cannot start a duplicate renderer, even when its archived setting remains enabled.
    /// </summary>
    [Test]
    public async Task LegacyBloomSystemsStayDisabled()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var originalEnabled = client.CfgMan.GetCVar(SunriseCCVars.LightBloomEnabled);
        try
        {
            await client.WaitPost(() => client.CfgMan.SetCVar(SunriseCCVars.LightBloomEnabled, true));
            await client.WaitAssertion(() =>
            {
                var systems = client.EntMan.EntitySysManager;
                var overlays = client.ResolveDependency<IOverlayManager>();
                Assert.That(systems.TryGetEntitySystem<LightingOverlaySystem>(out _), Is.False);
                Assert.That(systems.TryGetEntitySystem<BloomOverlayTreeSystem>(out _), Is.False);
                Assert.That(overlays.HasOverlay<PointLightingOverlay>(), Is.False);
                Assert.That(systems.TryGetEntitySystem<FishBloomSystem>(out _), Is.True);
            });
        }
        finally
        {
            await client.WaitPost(() => client.CfgMan.SetCVar(SunriseCCVars.LightBloomEnabled, originalEnabled));
        }

        await pair.CleanReturnAsync();
    }

    private static void AssertBloom(IOverlayManager overlays, bool enabled, float strength, FishBloomQuality quality)
    {
        if (!enabled || strength <= 0f)
        {
            Assert.That(overlays.HasOverlay<FishBloomOverlay>(), Is.False);
            return;
        }

        Assert.That(overlays.TryGetOverlay<FishBloomOverlay>(out var bloom), Is.True);
        Assert.That(bloom.Strength, Is.EqualTo(strength).Within(0.001f));
        Assert.That(bloom.Quality, Is.EqualTo(quality));
    }

    private static IEnumerable<Button> EnumerateButtons(Control parent)
    {
        foreach (var child in parent.Children)
        {
            if (child is Button button)
                yield return button;

            foreach (var nested in EnumerateButtons(child))
                yield return nested;
        }
    }

    private static async Task Click(TestPair pair, Control control)
    {
        foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
        {
            var position = control.PixelSize / 2f;
            await pair.Client.DoGuiEvent(control, new GUIBoundKeyEventArgs(
                EngineKeyFunctions.UIClick,
                state,
                new ScreenCoordinates(control.GlobalPixelPosition + position, control.Window?.Id ?? default),
                false,
                position / control.UIScale,
                position));
            await pair.RunTicksSync(1);
        }
    }
}
