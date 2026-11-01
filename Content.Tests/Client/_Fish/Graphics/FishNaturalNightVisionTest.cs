using System;
using System.Numerics;
using Content.Client._Fish.Graphics.Vision;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client._Fish.Graphics;

[TestFixture]
public sealed class FishNaturalNightVisionTest
{
    [TestCase(0d, 0.75f, 0.75f)]
    [TestCase(90d, 0.6875f, 1f / 6f)]
    [TestCase(180d, 0.25f, 0.25f)]
    public void LightingStaysCenteredOnTheEyeWhenTheCameraIsOffsetAndRotated(double degrees, float expectedX, float expectedY)
    {
        var bounds = new Box2Rotated(Box2.CenteredAround(new Vector2(10f, -20f), new Vector2(16f, 12f)),
            Angle.FromDegrees(degrees), new Vector2(10f, -20f));
        var geometry = FishNaturalNightVisionOverlay.GetLightingGeometry(in bounds, new Vector2(14f, -17f));
        Assert.That(geometry.X, Is.EqualTo(expectedX).Within(0.0001f));
        Assert.That(geometry.Y, Is.EqualTo(expectedY).Within(0.0001f));
        Assert.That(new Vector2(geometry.Z, geometry.W), Is.EqualTo(new Vector2(16f, 12f)));
    }

    [TestCase(1f)]
    [TestCase(2f)]
    [TestCase(0.5f)]
    public void LightingUsesWorldDistancesWhenZoomChanges(float zoom)
    {
        var bounds = new Box2Rotated(Box2.CenteredAround(Vector2.Zero, new Vector2(20f, 12f) * zoom));
        var geometry = FishNaturalNightVisionOverlay.GetLightingGeometry(in bounds, new Vector2(3.5f, 0f));
        var distanceFromCenter = (geometry.X - 0.5f) * geometry.Z;
        Assert.That(distanceFromCenter, Is.EqualTo(3.5f).Within(0.0001f),
            "The same 3.5-tile displacement must not change with the viewport's zoom.");
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(144)]
    public void DarkAdaptationReachesNinetyFivePercentInConfiguredTimeAtDifferentFrameRates(int framesPerSecond)
    {
        const float duration = 1.75f;
        var elapsed = 0f;
        var adapted = 0f;
        while (elapsed < duration)
        {
            var frame = Math.Min(1f / framesPerSecond, duration - elapsed);
            adapted += (1f - adapted) * FishNaturalNightVisionOverlay.GetAdaptationFactors(frame, 0.2f, duration).Y;
            elapsed += frame;
        }
        Assert.That(adapted, Is.EqualTo(0.95f).Within(0.001f));
    }

    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidOrBackwardsTimeDoesNotAdvanceAdaptation(float elapsed)
    {
        Assert.That(FishNaturalNightVisionOverlay.GetAdaptationFactors(elapsed, 0.2f, 1.75f), Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void InvalidConfigurationStillProducesFiniteResponseFactors()
    {
        var factors = FishNaturalNightVisionOverlay.GetAdaptationFactors(0.1f, float.NaN, -1f);
        Assert.That(factors.X, Is.InRange(0f, 1f));
        Assert.That(factors.Y, Is.InRange(0f, 1f));
        Assert.That(FishNaturalNightVisionOverlay.GetAdaptationFactors(0.1f, 0.2f, 1.75f).X,
            Is.GreaterThan(FishNaturalNightVisionOverlay.GetAdaptationFactors(0.1f, 0.2f, 1.75f).Y));
    }
}
