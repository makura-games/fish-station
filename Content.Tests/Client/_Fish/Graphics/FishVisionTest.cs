using Content.Client._Fish.Graphics.Vision;
using NUnit.Framework;

namespace Content.Tests.Client._Fish.Graphics;

[TestFixture]
public sealed class FishVisionTest
{
    [TestCase(309.15f)]
    [TestCase(310.15f)]
    [TestCase(312.15f)]
    public void BodyTemperatureIsBrightAndOpaqueInsteadOfRedTint(float kelvin)
    {
        var body = FishThermalPalette.GetColor(kelvin);
        Assert.That(body.A, Is.GreaterThanOrEqualTo(0.95f), "The cold background must not show through a warm body.");
        Assert.That(body.R, Is.GreaterThanOrEqualTo(0.95f));
        Assert.That(body.G, Is.GreaterThanOrEqualTo(0.9f), "Body heat should appear yellow-white, not red.");
        Assert.That(body.B, Is.GreaterThanOrEqualTo(0.6f));
    }

    [Test]
    public void HeatPaletteDistinguishesColdBodyAndHotTargets()
    {
        var cold = FishThermalPalette.GetColor(253.15f);
        var ambient = FishThermalPalette.GetColor(293.15f);
        var body = FishThermalPalette.GetColor(310.15f);
        var hot = FishThermalPalette.GetColor(373.15f);
        Assert.That(cold.B, Is.GreaterThan(cold.R));
        Assert.That(body.R, Is.GreaterThan(body.B));
        Assert.That(body.A, Is.GreaterThan(ambient.A));
        Assert.That(hot.G, Is.GreaterThan(body.G));
        Assert.That(hot.B, Is.GreaterThan(body.B));
        Assert.That(FishThermalPalette.GetColor(float.NaN).A, Is.Zero);
        Assert.That(FishThermalPalette.GetColor(float.PositiveInfinity).A, Is.Zero);
        Assert.That(FishThermalPalette.GetColor(-1).A, Is.Zero);
    }
}
