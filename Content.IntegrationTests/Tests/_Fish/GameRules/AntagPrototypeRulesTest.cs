using System;
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Roles;
using NUnit.Framework;

namespace Content.IntegrationTests.Tests._Fish.GameRules;

[TestFixture]
public sealed class AntagPrototypeRulesTest : GameTest
{
    private static readonly string[] _antagIds =
    [
        "AbductorVictim", "AssaultCommander", "AssaultOperative", "BloodCultist", "Changeling", "Dragon",
        "FleshCultist", "FleshCultistLeader", "Fugitive", "GenericAntagonist", "GenericFreeAgent",
        "GenericSiliconAntagonist", "GenericTeamAntagonist", "HeadRev", "InitialInfected", "LoneAbductorAntag",
        "MothershipCore", "Nukeops", "NukeopsCommander", "NukeopsMedic", "ParadoxClone", "PlanetPrisoner", "Rev",
        "SELFAgent", "SpaceNinja", "SubvertedSilicon", "Survivor", "Thief", "Traitor", "TraitorSleeper", "Wizard",
        "Xenoborg", "Zombie"
    ];

    private static readonly Dictionary<string, string> _antagRulesFromWiki = new()
    {
        ["BloodCultist"] = "fish-antag-rules-blood-cultist",
        ["Changeling"] = "fish-antag-rules-changeling",
        ["Dragon"] = "fish-antag-rules-cosmic-dragon",
        ["InitialInfected"] = "fish-antag-rules-zombie",
        ["LoneAbductorAntag"] = "fish-antag-rules-abductor",
        ["Nukeops"] = "fish-antag-rules-nukeops",
        ["NukeopsCommander"] = "fish-antag-rules-nukeops",
        ["NukeopsMedic"] = "fish-antag-rules-nukeops",
        ["Thief"] = "fish-antag-rules-thief",
        ["Traitor"] = "fish-antag-rules-traitor",
        ["TraitorSleeper"] = "fish-antag-rules-traitor",
        ["Zombie"] = "fish-antag-rules-zombie",
    };

    [Test]
    public async Task AntagonistPrototypeRulesMatchVerifiedWikiSources()
    {
        await Pair.Server.WaitAssertion(() =>
        {
            var antags = Pair.Server.ProtoMan
                .EnumeratePrototypes<AntagPrototype>()
                .ToDictionary(prototype => prototype.ID);

            // В пуле могут оставаться тестовые прототипы из других фикстур.
            Assert.That(antags.Keys.Where(id => !id.StartsWith("AntagTagTest", StringComparison.Ordinal)), Is.EquivalentTo(_antagIds));

            foreach (var id in _antagIds)
            {
                Assert.That(antags.TryGetValue(id, out var antag), Is.True, id);
                var expectedRules = _antagRulesFromWiki.GetValueOrDefault(id, string.Empty);
                Assert.That(antag.Rules?.Id ?? string.Empty, Is.EqualTo(expectedRules), antag.ID);
            }
        });
    }
}
