using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Roles;
using NUnit.Framework;

namespace Content.IntegrationTests.Tests._Fish.GameRules;

[TestFixture]
public sealed class AntagPrototypeRulesTest : GameTest
{
    private static readonly string[] AntagIds =
    [
        "AbductorVictim", "AssaultCommander", "AssaultOperative", "BloodCultist", "Changeling", "Dragon",
        "FleshCultist", "FleshCultistLeader", "Fugitive", "GenericAntagonist", "GenericFreeAgent",
        "GenericSiliconAntagonist", "GenericTeamAntagonist", "HeadRev", "InitialInfected", "LoneAbductorAntag",
        "MothershipCore", "Nukeops", "NukeopsCommander", "NukeopsMedic", "ParadoxClone", "PlanetPrisoner", "Rev",
        "SELFAgent", "SpaceNinja", "SubvertedSilicon", "Survivor", "Thief", "Traitor", "TraitorSleeper", "Wizard",
        "Xenoborg", "Zombie"
    ];

    private static readonly string[] AntagIdsWithWikiRules =
    [
        "BloodCultist", "Changeling", "Dragon", "InitialInfected", "LoneAbductorAntag", "Nukeops",
        "NukeopsCommander", "NukeopsMedic", "Thief", "Traitor", "TraitorSleeper", "Zombie"
    ];

    [Test]
    public async Task AntagonistPrototypeRulesMatchVerifiedWikiSources()
    {
        var antags = Pair.Server.ProtoMan
            .EnumeratePrototypes<AntagPrototype>()
            .ToDictionary(prototype => prototype.ID);

        await Pair.Server.WaitAssertion(() =>
        {
            foreach (var id in AntagIds)
            {
                Assert.That(antags.TryGetValue(id, out var antag), Is.True, id);
                var hasWikiRules = AntagIdsWithWikiRules.Contains(id);
                Assert.That(antag.Rules, hasWikiRules ? Is.Not.Empty : Is.Empty, antag.ID);
            }
        });
    }
}
