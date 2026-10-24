using System;
using Content.Shared._Fish.ClockworkCult;
using NUnit.Framework;

namespace Content.Tests._Fish.ClockworkCult;

[TestFixture]
[TestOf(typeof(FishClockworkCultConstants))]
public sealed class FishClockworkPowerTests
{
    [Test]
    public void UnlockThresholds_MatchTgstation()
    {
        Assert.That(FishClockworkCultConstants.ScriptUnlockThreshold, Is.EqualTo(50000f));
        Assert.That(FishClockworkCultConstants.ApplicationUnlockThreshold, Is.EqualTo(100000f));
        Assert.That(FishClockworkCultConstants.ArkGracePeriodSeconds, Is.EqualTo(300f));
        Assert.That(FishClockworkCultConstants.HeraldCostMultiplier, Is.EqualTo(0.75f));
        Assert.That(FishClockworkCultConstants.GatewayRatvarArrival, Is.EqualTo(600f));
    }

    [Test]
    public void HeraldCostReduction_IsTwentyFivePercent()
    {
        const float baseCost = 1000f;
        var heraldCost = baseCost * FishClockworkCultConstants.HeraldCostMultiplier;
        Assert.That(heraldCost, Is.EqualTo(750f));
    }

    [Test]
    public void ArkPrepMinutes_ClampsToMax()
    {
        Assert.That(FishClockworkCultConstants.CalculateArkPrepMinutes(0), Is.EqualTo(30));
        Assert.That(FishClockworkCultConstants.CalculateArkPrepMinutes(25), Is.EqualTo(35));
        Assert.That(FishClockworkCultConstants.CalculateArkPrepMinutes(100), Is.EqualTo(35));
    }
}
