using System;
using System.Collections.Generic;
using Content.Shared._Fish.Rpu;
using NUnit.Framework;

namespace Content.Tests._Fish.Rpu;

/// <summary>
/// Проверки аддитивного расчёта кармы: база нарушения + поправки в очках.
/// </summary>
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
        new RpuBanHistoryTier { MinBans = 1, Units = 1, ThresholdShift = 5 },
        new RpuBanHistoryTier { MinBans = 3, Units = 2, ThresholdShift = 10 },
        new RpuBanHistoryTier { MinBans = 6, Units = 3, ThresholdShift = 15 },
        new RpuBanHistoryTier { MinBans = 11, Units = 4, ThresholdShift = 20 },
    };

    [Test]
    public void PenaltyIsBasePlusUnits()
    {
        // Убийство экипажа стоит 6 очков, поправок нет — снимаем 6.
        Assert.That(RpuMaths.Penalty(6f, Array.Empty<float>()), Is.EqualTo(6f));
        Assert.That(RpuMaths.Penalty(6f, new[] { -2f }), Is.EqualTo(4f));
        Assert.That(RpuMaths.Penalty(6f, new[] { 2f, 1f }), Is.EqualTo(9f));
    }

    [Test]
    public void SeverityIsExtraUnits()
    {
        // Тяжесть случая — тоже очки, а не множитель.
        Assert.That(RpuMaths.Penalty(6f, Array.Empty<float>(), extra: 3f), Is.EqualTo(9f));
        Assert.That(RpuMaths.Penalty(6f, new[] { -1f }, extra: 2f), Is.EqualTo(7f));
    }

    [Test]
    public void PenaltyNeverGoesBelowMinimum()
    {
        // Сколько бы смягчающих очков ни набралось, вменяемое нарушение стоит
        // хотя бы одну десятую очка: ноль получает только полное освобождение (waives).
        Assert.That(RpuMaths.MinPenalty, Is.EqualTo(0.1f));
        Assert.That(RpuMaths.Penalty(2f, new[] { -1f, -2f, -3f }), Is.EqualTo(RpuMaths.MinPenalty));
        Assert.That(RpuMaths.Penalty(0.05f, Array.Empty<float>()), Is.EqualTo(RpuMaths.MinPenalty));
        Assert.That(RpuMaths.Penalty(0.5f, Array.Empty<float>()), Is.EqualTo(0.5f));
    }

    [Test]
    public void PenaltyNeverExceedsBasePlusCap()
    {
        var units = new[] { 4f, 4f, 4f, 10f };

        Assert.That(RpuMaths.Penalty(6f, units), Is.EqualTo(6f + RpuMaths.MaxExtraPenalty));
        Assert.That(RpuMaths.Penalty(6f, units, extra: 20f), Is.EqualTo(6f + RpuMaths.MaxExtraPenalty));
    }

    [Test]
    public void WaivedCategoryCostsNothing()
    {
        // Полное освобождение считается до формулы: там не снятие, а ноль.
        // Проверим, что формула сама по себе нуля не даёт — значит освобождение
        // должно решаться именно списком waives, а не вычитанием очков.
        Assert.That(RpuMaths.Penalty(1f, new[] { -100f }), Is.EqualTo(RpuMaths.MinPenalty));
    }

    [Test]
    public void KillSeriesUnits()
    {
        // Первые два убийства дешевле полной цены, дальше серия дорожит ступенями.
        Assert.That(RpuMaths.KillUnits(0), Is.EqualTo(0f));
        Assert.That(RpuMaths.KillUnits(1), Is.EqualTo(-3f));
        Assert.That(RpuMaths.KillUnits(2), Is.EqualTo(-1f));
        Assert.That(RpuMaths.KillUnits(3), Is.EqualTo(1f));
        Assert.That(RpuMaths.KillUnits(4), Is.EqualTo(3f));
        Assert.That(RpuMaths.KillUnits(5), Is.EqualTo(5f));
        Assert.That(RpuMaths.KillUnits(6), Is.EqualTo(5f));
        Assert.That(RpuMaths.KillUnits(7), Is.EqualTo(8f));
        Assert.That(RpuMaths.KillUnits(8), Is.EqualTo(10f));
        Assert.That(RpuMaths.KillUnits(9), Is.EqualTo(12f));
        Assert.That(RpuMaths.KillUnits(400), Is.EqualTo(12f));
    }

    [Test]
    public void BanHistoryTakesLastMatchingInterval()
    {
        Assert.That(RpuMaths.BanHistory(0, DefaultHistoryTiers()), Is.EqualTo((0f, 0f)));
        Assert.That(RpuMaths.BanHistory(1, DefaultHistoryTiers()), Is.EqualTo((1f, 5f)));
        Assert.That(RpuMaths.BanHistory(2, DefaultHistoryTiers()), Is.EqualTo((1f, 5f)));
        Assert.That(RpuMaths.BanHistory(6, DefaultHistoryTiers()), Is.EqualTo((3f, 15f)));
        Assert.That(RpuMaths.BanHistory(50, DefaultHistoryTiers()), Is.EqualTo((4f, 20f)));
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
    public void BanThresholdShiftBringsThresholdCloser()
    {
        // Сдвиг в очках поднимает порог к нулю: банить начинают раньше.
        Assert.That(RpuMaths.BanThreshold(2, DefaultTiers(), 20, false), Is.EqualTo(-20f));
    }

    [Test]
    public void BanThresholdShiftAppliesToLastResortTier()
    {
        // Игрок перешагнул все ступени: сдвиг истории банов обязан действовать и там,
        // иначе ветеранам с бэкграундом порог не ужесточается вовсе.
        var tiers = new List<RpuBanTier>
        {
            new() { MaxHours = 5, Threshold = -40 },
        };

        Assert.That(RpuMaths.BanThreshold(10, tiers, 10, true), Is.EqualTo(-90f));
        Assert.That(RpuMaths.BanThreshold(10, tiers, 0, true), Is.EqualTo(-100f));
        Assert.That(RpuMaths.BanThreshold(10, tiers, 10, false), Is.EqualTo(float.NegativeInfinity));
    }

    [Test]
    public void PenaltyHonoursConfiguredBounds()
    {
        // Рамки приходят из конфигов: проверяем, что они действительно работают,
        // и что перевёрнутые границы не роняют Math.Clamp.
        Assert.That(RpuMaths.Penalty(2f, new[] { 10f }, minPenalty: 0.5f, maxExtra: 1f), Is.EqualTo(3f));
        Assert.That(RpuMaths.Penalty(2f, new[] { -10f }, minPenalty: 0.5f, maxExtra: 1f), Is.EqualTo(0.5f));
        Assert.That(RpuMaths.Penalty(2f, new[] { 10f }, minPenalty: 0.5f, maxExtra: -1f), Is.EqualTo(2f));

        // Перевёрнутые границы (рамка больше потолка) не должны бросать исключение:
        // тогда верхом становится сама рамка.
        Assert.That(RpuMaths.Penalty(0.1f, new[] { 10f }, minPenalty: 5f, maxExtra: 0f), Is.EqualTo(5f));
    }

    [Test]
    public void ScaleByKarmaIsZeroAboveStart()
    {
        Assert.That(RpuMaths.ScaleByKarma(10, 0, -100, 0.1f, 0.5f), Is.EqualTo(0f));
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

    [Test]
    public void WholeViolationChain()
    {
        // Трейтор убил члена экипажа: база 10, льгота роли -2, третье убийство в раунде +1,
        // новичок +2 очка. Итого 10 - 2 + 1 + 2 = 11 очков.
        var units = new List<float> { -2f, RpuMaths.KillUnits(3), 2f };
        var penalty = RpuMaths.Penalty(10f, units);
        Assert.That(penalty, Is.EqualTo(11f));

        var karma = RpuMaths.ClampKarma(-1f - penalty);
        Assert.That(karma, Is.EqualTo(-12f));

        // Потолок надбавки к базе: 10 + 6 = 16, сколько бы очков ни наслоилось.
        var capped = RpuMaths.Penalty(10f, new[] { 40f });
        Assert.That(capped, Is.EqualTo(16f));
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
    [TestCase("99999999 years")]
    [TestCase("1e400 minutes")]
    [TestCase("-1")]                // минус — опечатка, а не «навсегда»
    [TestCase("-30 minutes")]
    public void BanDurationRejectsGarbage(string text)
    {
        // Переполнение TimeSpan должно возвращать отказ с текстом, а не исключение:
        // вызывающий код на отказе не банит вовсе.
        Assert.That(RpuBanDuration.TryParse(text, out _, out var error), Is.False);
        Assert.That(error, Is.Not.Null);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void BanDurationNeverRoundsDownToPermanent()
    {
        // Крохотный положительный срок не должен превратиться в бессрочный: его
        // поднимают до секунды, а не обнуляют.
        Assert.That(RpuBanDuration.TryParse("0.0000001 minutes", out var duration, out _), Is.True);
        Assert.That(duration, Is.EqualTo(TimeSpan.FromSeconds(1)));
    }

    /// <summary>
    /// Лестница убийств экипажа: капитан (отдел Command, −1 очко) без банов,
        /// базовая цена 6 очков + серия + очки за малое наигранное время.
    /// Проверено вручную и закреплено, чтобы изменение таблиц было видно сразу.
    /// </summary>
    [TestCase(0d, 4, -40f)]
    [TestCase(4d, 4, -40f)]
    [TestCase(7d, 5, -55f)]
    [TestCase(30d, 7, -70f)]
    public void KillLadderCrossesBanLimitOnExpectedKill(double roleHours, int expectedKill, float expectedLimit)
    {
        var tiers = new[]
        {
            new RpuBanTier { MaxHours = 5, Threshold = -40f },
            new RpuBanTier { MaxHours = 25, Threshold = -55f },
            new RpuBanTier { MaxHours = 60, Threshold = -70f },
            new RpuBanTier { MaxHours = 100000, Threshold = -100f, VeteransOnly = true },
        };

        var limit = RpuMaths.BanThreshold(roleHours, tiers, 0f, false);
        Assert.That(limit, Is.EqualTo(expectedLimit));

        // Очки за наигранное время: до 3 часов +3, до 10 часов +2, дальше ничего.
        var playtime = roleHours < 3 ? 3f : roleHours < 10 ? 2f : 0f;

        var karma = 0f;
        var crossed = 0;

        for (var kills = 1; kills <= 20 && crossed == 0; kills++)
        {
            var units = new List<float> { RpuMaths.KillUnits(kills), playtime, -1f };
            var penalty = RpuMaths.Penalty(10f, units);

            karma = RpuMaths.ClampKarma(karma - penalty);
            if (karma <= limit)
                crossed = kills;
        }

        Assert.That(crossed, Is.EqualTo(expectedKill),
            $"roleHours {roleHours}: limit {limit}, karma {karma}");
    }

    [Test]
    public void LossWindowSumsOnlyRecentLosses()
    {
        var now = TimeSpan.FromMinutes(10);
        var losses = new List<RpuMaths.RpuLoss>
        {
            new(TimeSpan.FromMinutes(1), 30f),
            new(TimeSpan.FromMinutes(4), 20f),
            new(TimeSpan.FromMinutes(9), 25f),
        };

        // Окно в 6 минут: младше него только потери на 4-й и 9-й минутах.
        Assert.That(RpuMaths.LossInWindow(losses, now, TimeSpan.FromMinutes(6)), Is.EqualTo(45f));
        Assert.That(RpuMaths.LossInWindow(losses, now, TimeSpan.FromMinutes(60)), Is.EqualTo(75f));
        Assert.That(RpuMaths.LossInWindow(new List<RpuMaths.RpuLoss>(), now, TimeSpan.FromMinutes(6)), Is.EqualTo(0f));
    }
}
