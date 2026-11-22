using System;
using System.Collections.Generic;
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

    private static List<PmAttempt> Attempts(int total, int realised, float error)
    {
        var list = new List<PmAttempt>();
        var start = new TimeSpan(0, 0, 0, 0);

        for (var i = 0; i < total; i++)
        {
            list.Add(new PmAttempt
            {
                At = start + TimeSpan.FromSeconds(i),
                Realised = i < realised,
                ErrorDegrees = i < realised ? error : float.NaN,
            });
        }

        return list;
    }

    [Test]
    public void RealisedShareNeedsEnoughSamples()
    {
        Assert.That(PmMaths.RealisedShare(Attempts(6, 6, 0.1f), 30, 12), Is.EqualTo(-1f));
    }

    [Test]
    public void ShareRejectsDegenerateWindow()
    {
        // Нулевой заказ или нулевое окно — это ошибка настройки, а не «взять всё»:
        // раньше на пустом списке attempts[^1] стрелял исключением.
        Assert.That(PmMaths.RealisedShare(Attempts(0, 0, 0.1f), 30, 0), Is.EqualTo(-1f));
        Assert.That(PmMaths.RealisedShare(Attempts(20, 20, 0.1f), 0, 12), Is.EqualTo(-1f));
        Assert.That(PmMaths.PerfectShareOfRealised(Attempts(0, 0, 0.1f), 0, 1f), Is.EqualTo(-1f));
    }

    [Test]
    public void RealisedShareCountsLandedShots()
    {
        Assert.That(PmMaths.RealisedShare(Attempts(20, 20, 0.1f), 60, 20), Is.EqualTo(1f));
        Assert.That(PmMaths.RealisedShare(Attempts(20, 10, 0.1f), 60, 20), Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void ExcludedAmbiguousShotsAreNotCountedAsMisses()
    {
        var attempts = Attempts(12, 9, 0.2f);
        for (var i = 9; i < 12; i++)
        {
            var attempt = attempts[i];
            attempt.ExcludedFromRatio = true;
            attempts[i] = attempt;
        }

        // При 3 неоднозначных и 9 известных попаданиях доля остаётся 100%, а не 75%.
        Assert.That(PmMaths.RealisedShare(attempts, 30, 9), Is.EqualTo(1f));
        Assert.That(PmMaths.PerfectShareOfRealised(attempts, 9, 1f), Is.EqualTo(1f));

        // Если известных попыток для порога уже не хватает, метрика ждёт новых данных.
        Assert.That(PmMaths.RealisedShare(attempts, 30, 10), Is.EqualTo(-1f));
    }

    /// <summary>Свежих попыток внутри окна мало — вывод не выносим.</summary>
    [Test]
    public void StaleSamplesAreNotEnoughEvidence()
    {
        // 20 попыток с шагом в секунду, окно 5 секунд: свежих всего шесть — этого мало.
        Assert.That(PmMaths.RealisedShare(Attempts(20, 12, 0.1f), 5, 20), Is.EqualTo(-1f));

        // То же, но окно покрывает всю серию.
        Assert.That(PmMaths.RealisedShare(Attempts(20, 12, 0.1f), 60, 20), Is.EqualTo(0.6f).Within(0.001f));
    }

    [Test]
    public void PerfectShareOnlyLooksAtLandedShots()
    {
        Assert.That(PmMaths.PerfectShareOfRealised(Attempts(12, 12, 0.4f), 12, 1.5f), Is.EqualTo(1f));
        Assert.That(PmMaths.PerfectShareOfRealised(Attempts(12, 12, 8f), 12, 1.5f), Is.EqualTo(0f));
        Assert.That(PmMaths.PerfectShareOfRealised(Attempts(12, 0, 0f), 12, 1.5f), Is.EqualTo(-1f));
    }
}
