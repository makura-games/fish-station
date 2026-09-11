using System.Numerics;
using Content.Client._Fish.Medical.Surgery;
using NUnit.Framework;
using System;
using Content.Client._Starlight.Medical.Surgery;
using Content.Shared.DoAfter;
using Content.Shared.Starlight.Medical.Surgery;
using Moq;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Tests._Fish.Medical.Surgery;

[TestFixture]
public sealed class FishSurgeryBodyDiagramTest
{
    [TestCase("SurgeryOpenIncision", "", true, true)]
    [TestCase("SurgeryCloseIncision", "", false, false)]
    [TestCase("SurgeryOpenIncision", "changed", false, false)]
    public void OperationRowsOnlyRebuildForStructuralChanges(string id, string suffix, bool completed, bool same)
    {
        (EntProtoId, string, bool)[] previous = [("SurgeryOpenIncision", "", false)];
        (EntProtoId, string, bool)[] current = [(id, suffix, completed)];
        Assert.That(SurgeryBui.HaveSameFishChoices(previous, current), Is.EqualTo(same));
        Assert.That(SurgeryBui.HaveSameFishChoices(previous, Array.Empty<(EntProtoId, string, bool)>()), Is.False);
    }

    // Проверяем не только туловище, но и маленькие области, которые легко перепутать при масштабировании.
    [TestCase("Head", 15, 6)]
    [TestCase("Torso", 15, 16)]
    [TestCase("ArmLeft", 23, 14)]
    [TestCase("ArmRight", 8, 14)]
    [TestCase("HandLeft", 23, 20)]
    [TestCase("HandRight", 8, 20)]
    [TestCase("LegLeft", 18, 26)]
    [TestCase("LegRight", 12, 26)]
    [TestCase("FootLeft", 19, 30)]
    [TestCase("FootRight", 11, 30)]
    public void BodyRegionsMatchAtDifferentSizes(string category, float x, float y)
    {
        foreach (var size in new[] { new Vector2(230, 330), new Vector2(460, 660), new Vector2(250, 480) })
        {
            var canvas = FishSurgeryBodyDiagram.GetCanvas(size);
            var pixel = canvas.TopLeft + new Vector2(x, y) * (canvas.Width / 32f);
            Assert.That(FishSurgeryBodyDiagram.HitTestRegion(pixel, size), Is.EqualTo(category));
        }
    }

    [TestCase(2, 16)]
    [TestCase(28, 16)]
    [TestCase(15.5f, 27)]
    [TestCase(15, 0)]
    public void EmptySpaceDoesNotSelectBody(float x, float y)
    {
        var size = new Vector2(230, 330);
        var canvas = FishSurgeryBodyDiagram.GetCanvas(size);
        var pixel = canvas.TopLeft + new Vector2(x, y) * (canvas.Width / 32f);
        Assert.That(FishSurgeryBodyDiagram.HitTestRegion(pixel, size), Is.Null);
    }

    [TestCase(23, 18, "HandLeft")]
    [TestCase(8, 18, "HandRight")]
    [TestCase(18, 29, "FootLeft")]
    [TestCase(12, 29, "FootRight")]
    public void SmallPartsWinAtSharedEdges(float x, float y, string category)
    {
        var size = new Vector2(230, 330);
        var canvas = FishSurgeryBodyDiagram.GetCanvas(size);
        var pixel = canvas.TopLeft + new Vector2(x, y) * (canvas.Width / 32f);
        Assert.That(FishSurgeryBodyDiagram.HitTestRegion(pixel, size), Is.EqualTo(category));
    }

    [TestCase(-1, 10, 0)]
    [TestCase(5, 10, 0.5f)]
    [TestCase(20, 10, 1)]
    [TestCase(0, 0, 1)]
    public void SurgicalProgressUsesActualDuration(double elapsed, float duration, float expected)
    {
        var patient = new EntityUid(2);
        var part = new EntityUid(3);
        var args = new DoAfterArgs(new Mock<IEntityManager>().Object, new EntityUid(1), duration,
            new SurgeryDoAfterEvent("TestSurgery", "TestStep", 1), patient, part);
        var action = new DoAfter(1, args, TimeSpan.FromSeconds(10));
        Assert.That(SurgeryBui.GetFishProgress(action, TimeSpan.FromSeconds(10 + elapsed)), Is.EqualTo(expected));
        Assert.That(SurgeryBui.IsFishSurgeryAction(action, patient, part, "TestSurgery", "TestStep"), Is.True);
        Assert.That(SurgeryBui.IsFishSurgeryAction(action, patient, new EntityUid(4), "TestSurgery", "TestStep"), Is.False);
    }

    [Test]
    public void SelectedStepUsesMatchingSurgeryAction()
    {
        var entities = new Mock<IEntityManager>().Object;
        var user = new EntityUid(1);
        var patient = new EntityUid(2);
        var selectedPart = new EntityUid(3);
        var otherPart = new EntityUid(4);
        var otherPartAction = CreateAction(1, otherPart, "TestSurgery", "SelectedStep");
        var otherStepAction = CreateAction(2, selectedPart, "TestSurgery", "OtherStep");
        var otherSurgeryAction = CreateAction(3, selectedPart, "OtherSurgery", "SelectedStep");
        var selectedAction = CreateAction(4, selectedPart, "TestSurgery", "SelectedStep");

        var result = SurgeryBui.FindFishSurgeryAction(
            new[] { otherPartAction, otherStepAction, otherSurgeryAction, selectedAction },
            patient,
            selectedPart,
            "TestSurgery",
            "SelectedStep");

        Assert.That(result, Is.SameAs(selectedAction));

        DoAfter CreateAction(ushort index, EntityUid part, EntProtoId surgery, EntProtoId step)
        {
            var args = new DoAfterArgs(entities, user, 10f,
                new SurgeryDoAfterEvent(surgery, step, 1f), patient, part);
            return new DoAfter(index, args, TimeSpan.FromSeconds(index));
        }
    }

    [Test]
    public void CancelledProgressDoesNotKeepFilling()
    {
        Assert.That(SurgeryBui.GetFishProgress(TimeSpan.Zero, TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(20)), Is.EqualTo(0.3f));
    }
}
