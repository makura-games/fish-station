using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Content.Shared._Fish.Achievements;
using Moq;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.Tests.Shared._Fish.Achievements;

[TestFixture]
public sealed class AchievementCatalogStatsTests
{
    private static void SetPrototypeId<T>(T proto, string id) where T : IPrototype
    {
        typeof(T).GetProperty(nameof(IPrototype.ID), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(proto, id);
    }

    private static AchievementPrototype Proto(
        string id,
        string category,
        string condition = AchievementConditionKeys.Kill,
        int order = 0)
    {
#pragma warning disable RA0039 // unit-test stubs без PrototypeManager pool
        var proto = new AchievementPrototype
        {
            Category = category,
            Condition = condition,
            Order = order,
        };
#pragma warning restore RA0039
        SetPrototypeId(proto, id);
        return proto;
    }

    private static AchievementCategoryPrototype Category(string id, int order = 0)
    {
#pragma warning disable RA0039
        var cat = new AchievementCategoryPrototype
        {
            Name = $"fish-achievements-category-{id.ToLowerInvariant()}",
            Order = order,
        };
#pragma warning restore RA0039
        SetPrototypeId(cat, id);
        return cat;
    }

    private static IPrototypeManager CreateProtoMan(IEnumerable<AchievementPrototype> achievements)
    {
        return CreateProtoMan(achievements, Array.Empty<AchievementCategoryPrototype>());
    }

    private static IPrototypeManager CreateProtoMan(
        IEnumerable<AchievementPrototype> achievements,
        IEnumerable<AchievementCategoryPrototype> categories)
    {
        var achList = achievements.ToList();
        var catList = categories.ToList();

        var mock = new Mock<IPrototypeManager>();
        mock.Setup(m => m.EnumeratePrototypes<AchievementPrototype>()).Returns(achList);
        mock.Setup(m => m.EnumeratePrototypes<AchievementCategoryPrototype>()).Returns(catList);
        return mock.Object;
    }

    [Test]
    public void AllCategory_IncludesEveryVisibleAchievement()
    {
        var states = new Dictionary<string, AchievementPlayerState>
        {
            ["A"] = new("A", 0, true, null),
        };

        var protoMan = CreateProtoMan(new[]
        {
            Proto("A", "FishAchCombat"),
            Proto("B", "FishAchMisc"),
            Proto("Manual", "FishAchMisc", AchievementConditionKeys.Manual),
        });

        var all = AchievementCatalogStats.EnumerateVisible(protoMan, states, AchievementCatalogStats.AllCategoriesId).ToList();
        Assert.That(all, Has.Count.EqualTo(2));
        Assert.That(all.Select(p => p.ID), Is.EquivalentTo(new[] { "A", "B" }));
    }

    [Test]
    public void ManualStub_HiddenUntilProgress()
    {
        var states = new Dictionary<string, AchievementPlayerState>();
        var protoMan = CreateProtoMan(new[] { Proto("Manual", "FishAchMisc", AchievementConditionKeys.Manual) });

        Assert.That(
            AchievementCatalogStats.EnumerateVisible(protoMan, states, AchievementCatalogStats.AllCategoriesId).Any(),
            Is.False);

        states["Manual"] = new("Manual", 1, false, null);
        Assert.That(
            AchievementCatalogStats.EnumerateVisible(protoMan, states, AchievementCatalogStats.AllCategoriesId).Count(),
            Is.EqualTo(1));
    }

    [Test]
    public void CountByCategory_ComputesTotalsDynamically()
    {
        var states = new Dictionary<string, AchievementPlayerState>
        {
            ["A"] = new("A", 0, true, null),
            ["B"] = new("B", 0, false, null),
        };

        var protoMan = CreateProtoMan(
            new[]
            {
                Proto("A", "FishAchCombat"),
                Proto("B", "FishAchCombat"),
                Proto("C", "FishAchMisc"),
            },
            new[]
            {
                Category("FishAchCombat", 10),
                Category("FishAchMisc", 100),
            });

        var rows = AchievementCatalogStats.CountByCategory(protoMan, states);
        var combat = rows.First(r => r.CategoryId == "FishAchCombat");
        var misc = rows.First(r => r.CategoryId == "FishAchMisc");

        Assert.That(combat.Unlocked, Is.EqualTo(1));
        Assert.That(combat.Total, Is.EqualTo(2));
        Assert.That(combat.Percent, Is.EqualTo(50));
        Assert.That(misc.Total, Is.EqualTo(1));
        Assert.That(misc.Unlocked, Is.EqualTo(0));
    }

    [Test]
    public void SelectionProgress_AllAndCategory_AreIndependent()
    {
        var states = new Dictionary<string, AchievementPlayerState>
        {
            ["A"] = new("A", 0, true, null),
            ["B"] = new("B", 0, false, null),
        };

        var protoMan = CreateProtoMan(
            new[]
            {
                Proto("A", "FishAchCombat"),
                Proto("B", "FishAchCombat"),
                Proto("C", "FishAchMisc"),
            },
            new[]
            {
                Category("FishAchCombat", 10),
                Category("FishAchMisc", 100),
            });

        var all = AchievementCatalogStats.GetSelectionProgress(protoMan, states, AchievementCatalogStats.AllCategoriesId);
        Assert.That(all.Unlocked, Is.EqualTo(1));
        Assert.That(all.Total, Is.EqualTo(3));
        Assert.That(all.Percent, Is.EqualTo(33));

        var combat = AchievementCatalogStats.GetSelectionProgress(protoMan, states, "FishAchCombat");
        Assert.That(combat.Unlocked, Is.EqualTo(1));
        Assert.That(combat.Total, Is.EqualTo(2));
        Assert.That(combat.Percent, Is.EqualTo(50));
        Assert.That(combat.CategoryId, Is.EqualTo("FishAchCombat"));
    }

    [Test]
    public void CatalogBaseline_Has117Achievements()
    {
        var auditPath = Path.Combine(FindRepoRoot(), "Resources", "Docs", "_Fish", "AchievementsTriggerAudit.json");
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(auditPath));
        Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(117));
    }

    private static string FindRepoRoot()
    {
        // Content Tests в CI идут из артефакта без полного checkout: обходим несколько стартовых точек.
        foreach (var start in new[]
                 {
                     TestContext.CurrentContext.TestDirectory,
                     Directory.GetCurrentDirectory(),
                     AppContext.BaseDirectory,
                     Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                 })
        {
            var dir = start;
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.Exists(Path.Combine(dir, "Resources", "Prototypes", "_Fish", "Achievements")))
                    return dir;

                dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
            }
        }

        throw new InvalidOperationException("repo root not found");
    }
}
