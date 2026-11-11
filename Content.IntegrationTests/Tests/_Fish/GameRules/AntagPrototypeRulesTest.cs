using System;
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Roles;
using Content.Shared._Fish.Antag;
using Content.Shared.Mind;
using Content.Shared.Players;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests._Fish.GameRules;

[TestFixture]
public sealed class AntagPrototypeRulesTest : GameTest, IEntityEventSubscriber
{
    private readonly List<FishAntagRulesEvent> _receivedRules = new();

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        DummyTicker = false,
        Dirty = true,
    };

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

    /// <summary>
    /// Проверяет, что ключи правил антагонистов совпадают с подтверждёнными источниками Fish Wiki.
    /// </summary>
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

    /// <summary>
    /// Проверяет повторную отправку правил после переподключения к тому же разуму.
    /// </summary>
    [Test]
    public async Task AntagonistRulesAreShownAgainForANewSession()
    {
        await Client.WaitPost(() => Client.EntMan.EventBus.SubscribeEvent<FishAntagRulesEvent>(
            EventSource.Network,
            this,
            OnRulesReceived));

        try
        {
            var playerManager = Server.ResolveDependency<IPlayerManager>();
            var session = playerManager.Sessions.Single();
            var username = session.Name;

            await Server.WaitAssertion(() =>
            {
                var mindId = session.ContentData()!.Mind!.Value;
                var mind = Server.EntMan.GetComponent<MindComponent>(mindId);
                Server.System<RoleSystem>().MindAddRole(mindId, "MindRoleTraitor", mind, silent: true);
            });
            await RunTicksSync(5);
            await Client.WaitAssertion(() => Assert.That(_receivedRules, Has.Count.EqualTo(1)));

            var netManager = Client.ResolveDependency<IClientNetManager>();
            await Client.WaitAssertion(() => netManager.ClientDisconnect("Antagonist rules reconnect test"));
            await RunTicksSync(5);

            Client.SetConnectTarget(Server);
            var baseClient = Client.ResolveDependency<Robust.Client.IBaseClient>();
            await Client.WaitPost(() =>
            {
                baseClient.PlayerNameOverride = username;
                baseClient.ConnectToServer(new System.Net.DnsEndPoint("localhost", 1212));
            });
            await RunTicksSync(10);

            await Client.WaitAssertion(() => Assert.That(_receivedRules, Has.Count.EqualTo(2)));
            Assert.That(_receivedRules[1].Rules, Is.EqualTo(new[] { "fish-antag-rules-traitor" }));
        }
        finally
        {
            await Client.WaitPost(() => Client.EntMan.EventBus.UnsubscribeEvents(this));
        }
    }

    private void OnRulesReceived(FishAntagRulesEvent args)
    {
        _receivedRules.Add(args);
    }
}
