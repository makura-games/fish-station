using System;
using System.Collections.Generic;
using Content.Shared._Fish.Rpu;
using NUnit.Framework;

namespace Content.Tests._Fish.Rpu;

[TestFixture]
public sealed class RpuMathsTest
{
    private static List<RpuBanTier> DefaultTiers() => new()
    {
        new RpuBanTier { MaxHours = 5, Threshold = -40 },
        new RpuBanTier { MaxHours = 25, Threshold = -55 },
        new RpuBanTier { MaxHours = 60, Threshold = -70 },
        new RpuBanTier { MaxHours = 120, Threshold = -85 },
        new RpuBanTier { MaxHours = 200, Threshold = -95 },
        new RpuBanTier { MaxHours = 100000, Threshold = -100, VeteransOnly = true },
    };

    private static List<RpuBanHistoryTier> DefaultHistoryTiers() => new()
    {
        new RpuBanHistoryTier { MinBans = 1, Multiplier = 1.1f, ThresholdShift = 5 },
        new RpuBanHistoryTier { MinBans = 3, Multiplier = 1.25f, ThresholdShift = 10 },
        new RpuBanHistoryTier { MinBans = 6, Multiplier = 1.4f, ThresholdShift = 15 },
        new RpuBanHistoryTier { MinBans = 11, Multiplier = 1.5f, ThresholdShift = 20 },
    };

    [Test]
    public void CombineMultipliersEmptyIsNeutral()
    {
        Assert.That(RpuMaths.CombineMultipliers(Array.Empty<float>()), Is.EqualTo(1f));
    }

    [Test]
    public void CombineMultipliersZeroMeansFullImmunity()
    {
        Assert.That(RpuMaths.CombineMultipliers(new[] { 0.9f, 0f, 0.5f }), Is.EqualTo(0f));
    }

    [Test]
    public void CombineMultipliersDoesNotMultiplyAway()
    {
        var combined = RpuMaths.CombineMultipliers(new[] { 0.6f, 0.6f, 0.6f, 0.6f, 0.6f });

        // Пять одинаковых льгот не должны превращаться в произведение (0.078).
        Assert.That(combined, Is.EqualTo(0.6f).Within(0.001f));
        Assert.That(combined, Is.GreaterThan(0.078f));
    }

    [Test]
    public void CombineMultipliersIgnoresArgumentOrder()
    {
        var strong = RpuMaths.CombineMultipliers(new[] { 0.2f, 0.95f });
        var neutral = RpuMaths.CombineMultipliers(new[] { 0.95f, 0.95f });

        Assert.That(strong, Is.LessThan(neutral));
        Assert.That(RpuMaths.CombineMultipliers(new[] { 0.95f, 0.2f }), Is.EqualTo(strong).Within(0.001f));
    }

    [Test]
    public void CombineMultipliersClampsToFloorAndCap()
    {
        Assert.That(RpuMaths.CombineMultipliers(new[] { 0.01f }), Is.EqualTo(0.15f));
        Assert.That(RpuMaths.CombineMultipliers(new[] { 9f }), Is.EqualTo(1.5f));
    }

    [Test]
    public void KillEscalationCurve()
    {
        Assert.That(RpuMaths.KillEscalation(0), Is.EqualTo(0.5f));
        Assert.That(RpuMaths.KillEscalation(2), Is.EqualTo(0.5f));
        Assert.That(RpuMaths.KillEscalation(3), Is.EqualTo(1f));
        Assert.That(RpuMaths.KillEscalation(6), Is.EqualTo(1f));
        Assert.That(RpuMaths.KillEscalation(7), Is.EqualTo(1.2f));
        Assert.That(RpuMaths.KillEscalation(13), Is.EqualTo(1.5f));
        Assert.That(RpuMaths.KillEscalation(40), Is.EqualTo(1.5f));
    }

    [Test]
    public void BanThresholdByRoleHours()
    {
        var tiers = DefaultTiers();

        Assert.That(RpuMaths.BanThreshold(2, tiers, 0, false), Is.EqualTo(-40f));
        Assert.That(RpuMaths.BanThreshold(30, tiers, 0, false), Is.EqualTo(-70f));
        Assert.That(RpuMaths.BanThreshold(250, tiers, 0, false), Is.EqualTo(float.NegativeInfinity));
        Assert.That(RpuMaths.BanThreshold(250, tiers, 0, true), Is.EqualTo(-100f));
    }

    [Test]
    public void BanThresholdShiftMakesBansEasier()
    {
        // Сдвиг поднимает порог к нулю: банить начинают раньше.
        Assert.That(RpuMaths.BanThreshold(2, DefaultTiers(), 20, false), Is.EqualTo(-20f));
    }

    [Test]
    public void BanHistoryPicksLastMatchingTier()
    {
        Assert.That(RpuMaths.BanHistoryModifiers(0, DefaultHistoryTiers()), Is.EqualTo((1f, 0f)));
        Assert.That(RpuMaths.BanHistoryModifiers(2, DefaultHistoryTiers()), Is.EqualTo((1.1f, 5f)));
        Assert.That(RpuMaths.BanHistoryModifiers(6, DefaultHistoryTiers()), Is.EqualTo((1.4f, 15f)));
        Assert.That(RpuMaths.BanHistoryModifiers(50, DefaultHistoryTiers()), Is.EqualTo((1.5f, 20f)));
    }

    [Test]
    public void ScaleByKarmaIsZeroAboveStart()
    {
        Assert.That(RpuMaths.ScaleByKarma(10, 0, -100, 0.1f, 0.5f), Is.EqualTo(0f));
        Assert.That(RpuMaths.ScaleByKarma(0, 0, -100, 0.1f, 0.5f), Is.EqualTo(0f));
        Assert.That(RpuMaths.ScaleByKarma(-100, 0, -100, 0.1f, 0.5f), Is.EqualTo(0.5f));
        Assert.That(RpuMaths.ScaleByKarma(-50, 0, -100, 0.1f, 0.5f), Is.EqualTo(0.3f).Within(0.001f));
    }

    [Test]
    public void ClampKarma()
    {
        Assert.That(RpuMaths.ClampKarma(-500), Is.EqualTo(RpuMaths.KarmaMin));
        Assert.That(RpuMaths.ClampKarma(500), Is.EqualTo(RpuMaths.KarmaMax));
        Assert.That(RpuMaths.ClampKarma(-3.5f), Is.EqualTo(-3.5f));
    }

    [TestCase("0", null)]
    [TestCase("", null)]
    [TestCase("30", 30)]
    [TestCase("6 hours", 360)]
    [TestCase("3 дня", 4320)]
    [TestCase("1 year", 525600)]
    [TestCase("2 weeks", 20160)]
    public void BanDurationParsing(string text, int? expectedMinutes)
    {
        Assert.That(RpuBanDuration.TryParse(text, out var duration, out var error), Is.True, error);

        if (expectedMinutes is null)
        {
            Assert.That(duration, Is.Null);
            return;
        }

        Assert.That(duration, Is.Not.Null);
        Assert.That(duration.Value.TotalMinutes, Is.EqualTo(expectedMinutes.Value));
    }

    [TestCase("abc")]
    [TestCase("5 parsecs")]
    [TestCase("1 2 3")]
    public void BanDurationRejectsGarbage(string text)
    {
        Assert.That(RpuBanDuration.TryParse(text, out _, out var error), Is.False);
        Assert.That(error, Is.Not.Null);
        Assert.That(error, Is.Not.Empty);
    }
}
