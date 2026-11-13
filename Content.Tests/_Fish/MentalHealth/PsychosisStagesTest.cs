using Content.Shared._Fish.Psychosis;
using NUnit.Framework;

namespace Content.Tests._Fish.MentalHealth;

[TestFixture]
public sealed class PsychosisStagesTest
{
    [Test]
    public void CalmBelowFirstThreshold()
    {
        Assert.That(PsychosisStages.Get(0f), Is.EqualTo(PsychosisStage.Calm));
        Assert.That(PsychosisStages.Get(PsychosisStages.UnsettledMin - 0.1f), Is.EqualTo(PsychosisStage.Calm));
    }

    [Test]
    public void StageChangesAtThresholds()
    {
        Assert.That(PsychosisStages.Get(PsychosisStages.UnsettledMin), Is.EqualTo(PsychosisStage.Unsettled));
        Assert.That(PsychosisStages.Get(PsychosisStages.DistortedMin), Is.EqualTo(PsychosisStage.Distorted));
        Assert.That(PsychosisStages.Get(PsychosisStages.DeliriousMin), Is.EqualTo(PsychosisStage.Delirious));
        Assert.That(PsychosisStages.Get(PsychosisStages.TerminalMin), Is.EqualTo(PsychosisStage.Terminal));
    }

    [Test]
    public void MaximumLevelIsTerminal()
    {
        Assert.That(PsychosisStages.Get(100f), Is.EqualTo(PsychosisStage.Terminal));
    }

    [Test]
    public void ThresholdsAreOrdered()
    {
        Assert.That(PsychosisStages.UnsettledMin, Is.LessThan(PsychosisStages.DistortedMin));
        Assert.That(PsychosisStages.DistortedMin, Is.LessThan(PsychosisStages.DeliriousMin));
        Assert.That(PsychosisStages.DeliriousMin, Is.LessThan(PsychosisStages.TerminalMin));
    }
}
