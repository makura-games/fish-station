using Content.Client._Fish.Anomaly;
using Content.Shared.Anomaly.Effects.Components;
using NUnit.Framework;

namespace Content.Tests._Fish.Anomaly;

[TestFixture]
public sealed class GravityAnomalyDustTest
{
    [TestCase(2f, 3f, true, true)]
    [TestCase(-2f, -3f, false, false)]
    public void DustMatchesTheDirectionOfTheGravityWell(float radial, float tangential, bool inward, bool counterClockwise)
    {
        var field = new GravityAnomalyField(0, 5, radial, tangential);
        var point = GravityAnomalyDustOverlay.Streamline(field, 4, 0, 0.4f);
        Assert.That(point.Length() < 2f, Is.EqualTo(inward));
        Assert.That(point.Y > 0, Is.EqualTo(counterClockwise));
    }

    [Test]
    public void PureRotationKeepsRadiusAndPureAttractionEndsAtTheCenter()
    {
        var tangent = new GravityAnomalyField(0, 4, 0, 2);
        var point = GravityAnomalyDustOverlay.Streamline(tangent, 4, 0, 0.5f);
        Assert.That(point.Length(), Is.EqualTo(2f).Within(0.0001f));
        Assert.That(point.Y, Is.Positive);

        var inward = new GravityAnomalyField(0, 4, 2, 0);
        Assert.That(GravityAnomalyDustOverlay.Streamline(inward, 0.12f, 0, 1f).Length(), Is.Zero);
    }
}
