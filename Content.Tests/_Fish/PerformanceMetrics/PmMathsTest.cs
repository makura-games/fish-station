using System;
using Content.Server._Fish.PerformanceMetrics;
using NUnit.Framework;

namespace Content.Tests._Fish.PerformanceMetrics;

[TestFixture]
public sealed class PmMathsTest
{
    [Test]
    public void AngularErrorZeroWhenFacingTarget()
    {
        Assert.That(PmMaths.AngularErrorDegrees(1.0, 1.0), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void AngularErrorRightAngle()
    {
        Assert.That(PmMaths.AngularErrorDegrees(0.0, Math.PI / 2.0), Is.EqualTo(90f).Within(0.001f));
    }

    /// <summary>
    /// Разворот на 350 градусов — это не «почти совпало»: ошибка должна считаться
    /// по короткой дуге, иначе автоприцел с постоянным доворотом выглядел бы идеально.
    /// </summary>
    [Test]
    public void AngularErrorUsesShortArc()
    {
        var facing = Math.PI;
        var target = -Math.PI + Math.PI / 18.0; // 10 градусов по короткой дуге

        Assert.That(PmMaths.AngularErrorDegrees(facing, target), Is.EqualTo(10f).Within(0.05f));
    }

    [Test]
    public void AngularErrorNeverExceedsHalfTurn()
    {
        Assert.That(PmMaths.AngularErrorDegrees(0.0, Math.PI * 1.9), Is.LessThanOrEqualTo(180.01f));
    }

    [Test]
    public void RegularCadenceScoresLowVariation()
    {
        Span<float> gaps = stackalloc float[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };

        Assert.That(PmMaths.VariationCoefficient(gaps), Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void JitteredCadenceScoresHighVariation()
    {
        Span<float> gaps = stackalloc float[] { 0.2f, 0.9f, 0.35f, 1.4f, 0.4f };

        Assert.That(PmMaths.VariationCoefficient(gaps), Is.GreaterThan(0.6f));
    }

    [Test]
    public void TooFewSamplesHaveNoOpinion()
    {
        Span<float> gaps = stackalloc float[] { 0.5f, 0.5f };

        Assert.That(PmMaths.VariationCoefficient(gaps), Is.EqualTo(float.PositiveInfinity));
    }

    [Test]
    public void SpeedExcessIsNegativeInsideBudget()
    {
        var excess = PmMaths.SpeedExcessTiles(movedTiles: 4f, allowedSpeed: 4.5f, seconds: 0.5f, margin: 3.0f);

        Assert.That(excess, Is.LessThan(0f));
    }

    [Test]
    public void SpeedExcessCountsTilesAboveBudget()
    {
        var excess = PmMaths.SpeedExcessTiles(movedTiles: 20f, allowedSpeed: 4.5f, seconds: 0.5f, margin: 3.0f);

        Assert.That(excess, Is.EqualTo(13.25f).Within(0.01f));
    }

    [Test]
    public void ZeroWindowYieldsNoExcess()
    {
        Assert.That(PmMaths.SpeedExcessTiles(50f, 4.5f, 0f, 3.0f), Is.EqualTo(0f));
    }

    [Test]
    public void ActionsPerSecondScalesByWindow()
    {
        Assert.That(PmMaths.ActionsPerSecond(44, 2f), Is.EqualTo(22f));
        Assert.That(PmMaths.ActionsPerSecond(10, 0f), Is.EqualTo(0f));
    }

    /// <summary>Признаки независимы: суммируем количество, а не их силу.</summary>
    [Test]
    public void ScoreCountsDistinctSignals()
    {
        Assert.That(PmMaths.Score(1, 0, 0, 0, 0), Is.EqualTo(1));
        Assert.That(PmMaths.Score(99, 99, 99, 99, 99), Is.EqualTo(5));
        Assert.That(PmMaths.Score(0, 0, 0, 0, 0), Is.EqualTo(0));
    }
}
