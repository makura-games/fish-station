using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Fish.ObrCall;
using Content.Server.AlertLevel;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Revolutionary.Components;
using Content.Server.Station.Systems;
using Content.Server._Sunrise.Storyteller.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Station.Components;
using Content.Shared._Fish.ObrCall;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Fish.ObrCall;

[TestFixture]
[TestOf(typeof(ObrCallSystem))]
public sealed class RevolutionGammaErtTest : GameTest
{
    private const string RevolutionRuleId = "Revolutionary";
    private const string GammaTeamId = "ObrGamma";

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Map = PoolManager.TestStation,
    };

    /// <summary>
    /// Шесть глав мертвы, седьмой жив и закован — вызывается ОБР Гамма, код Гамма, раунд продолжается.
    /// </summary>
    [Test]
    public async Task DetainedCommandDeploysGammaWithoutEndingRound()
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();
        var obr = server.System<ObrCallSystem>();
        var mobState = server.System<MobStateSystem>();
        var stationSystem = server.System<StationSystem>();
        var alertLevel = server.System<AlertLevelSystem>();
        var ghostRoleSystem = server.System<GhostRoleSystem>();
        var containers = server.System<SharedContainerSystem>();

        EntityUid station = default;

        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.StartGameRule(RevolutionRuleId), Is.True, "Failed to start the revolution rule");

            station = stationSystem.GetStations().First(s => entMan.HasComponent<MainStationComponent>(s));
            var stationData = entMan.GetComponent<StationDataComponent>(station);
            var grid = stationData.Grids.First();

            for (var i = 0; i < 7; i++)
            {
                var head = entMan.SpawnEntity("MobHuman", new EntityCoordinates(grid, i, 0));
                entMan.AddComponent<CommandStaffComponent>(head);

                if (i < 6)
                {
                    mobState.ChangeMobState(head, MobState.Dead);
                    continue;
                }

                // Последний глава жив, но закован.
                var cuffs = entMan.SpawnEntity("Handcuffs", new EntityCoordinates(grid, i, 0));
                var cuffable = entMan.GetComponent<CuffableComponent>(head);
                Assert.That(containers.Insert(cuffs, cuffable.Container), Is.True, "Failed to cuff the last command member");
            }
        });

        await server.WaitRunTicks(20);

        await server.WaitAssertion(() =>
        {
            EntityUid gammaGrid = default;
            var calledQuery = entMan.EntityQueryEnumerator<ObrCalledShuttleComponent>();
            while (calledQuery.MoveNext(out var gridUid, out var called))
            {
                if (called.TeamId == GammaTeamId)
                {
                    gammaGrid = gridUid;
                    break;
                }
            }

            var ghostRoles = 0;
            var ghostRoleQuery = entMan.EntityQueryEnumerator<GhostRoleComponent>();
            while (ghostRoleQuery.MoveNext(out var roleUid, out _))
            {
                if (entMan.GetComponent<TransformComponent>(roleUid).GridUid == gammaGrid)
                    ghostRoles++;
            }

            var registered = 0;
            var paused = 0;
            var raffled = 0;
            var identifiers = new HashSet<uint>();
            foreach (var roleEnt in ghostRoleSystem.GhostRoles)
            {
                if (entMan.GetComponent<TransformComponent>(roleEnt.Owner).GridUid != gammaGrid)
                    continue;

                registered++;
                identifiers.Add(roleEnt.Comp.Identifier);

                if (entMan.GetComponent<MetaDataComponent>(roleEnt.Owner).EntityPaused)
                    paused++;

                if (roleEnt.Comp.RaffleConfig != null)
                    raffled++;
            }

            var visible = ghostRoleSystem.GetGhostRolesInfo(null)
                .Count(info => identifiers.Contains(info.Identifier));

            Assert.Multiple(() =>
            {
                Assert.That(gammaGrid, Is.Not.EqualTo(EntityUid.Invalid), "Gamma ERT shuttle was not found");
                Assert.That(obr.IsTeamActive(GammaTeamId), Is.True, "Gamma ERT was not called after the command was detained");
                Assert.That(ghostRoles, Is.GreaterThanOrEqualTo(16), "Gamma ERT shuttle roles are not ghost roles");
                Assert.That(registered, Is.GreaterThanOrEqualTo(16), $"Gamma ERT roles are not registered as ghost roles (components={ghostRoles}, registered={registered}, paused={paused}, visible={visible})");
                Assert.That(paused, Is.EqualTo(0), $"Gamma ERT ghost roles are paused (components={ghostRoles}, registered={registered}, paused={paused}, visible={visible})");
                Assert.That(visible, Is.GreaterThanOrEqualTo(16), $"Gamma ERT ghost roles are not visible in the ghost role menu (components={ghostRoles}, registered={registered}, paused={paused}, visible={visible})");
                Assert.That(raffled, Is.EqualTo(0), $"Gamma ERT ghost roles should not use raffles (raffled={raffled})");
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound), "Round ended after the command was detained");
                Assert.That(alertLevel.GetLevel(station), Is.EqualTo("gamma"), "Gamma alert was not set");
            });
        });
    }

    /// <summary>
    /// Все главы мертвы — как раньше ставится код Эпсилон и раунд завершается, ОБР Гамма не вызывается.
    /// </summary>
    [Test]
    public async Task DeadCommandSetsEpsilonAndEndsRound()
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();
        var obr = server.System<ObrCallSystem>();
        var mobState = server.System<MobStateSystem>();
        var stationSystem = server.System<StationSystem>();
        var alertLevel = server.System<AlertLevelSystem>();

        EntityUid station = default;

        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.StartGameRule(RevolutionRuleId), Is.True, "Failed to start the revolution rule");

            station = stationSystem.GetStations().First(s => entMan.HasComponent<MainStationComponent>(s));
            var stationData = entMan.GetComponent<StationDataComponent>(station);

            var command = entMan.SpawnEntity("MobHuman", new EntityCoordinates(stationData.Grids.First(), 0, 0));
            entMan.AddComponent<CommandStaffComponent>(command);
            mobState.ChangeMobState(command, MobState.Dead);
        });

        await server.WaitRunTicks(20);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                // Эпсилон — дополнительный код, поэтому он не заменяет основной уровень и проверяется через IsLevelActive.
                Assert.That(alertLevel.IsLevelActive(station, "epsilon"), Is.True, "Epsilon alert was not set after all command died");
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PostRound), "Round did not end after all command died");
                Assert.That(obr.IsTeamActive(GammaTeamId), Is.False, "Gamma ERT should not be called when all command died");
            });
        });
    }

    /// <summary>
    /// Живой незакованный глава не даёт вызвать ОБР Гамма, даже если остальные главы мертвы.
    /// </summary>
    [Test]
    public async Task AliveUncuffedCommandDoesNotDeployGamma()
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();
        var obr = server.System<ObrCallSystem>();
        var mobState = server.System<MobStateSystem>();
        var stationSystem = server.System<StationSystem>();
        var alertLevel = server.System<AlertLevelSystem>();

        EntityUid station = default;

        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.StartGameRule(RevolutionRuleId), Is.True, "Failed to start the revolution rule");

            station = stationSystem.GetStations().First(s => entMan.HasComponent<MainStationComponent>(s));
            var stationData = entMan.GetComponent<StationDataComponent>(station);
            var grid = stationData.Grids.First();

            var deadHead = entMan.SpawnEntity("MobHuman", new EntityCoordinates(grid, 0, 0));
            entMan.AddComponent<CommandStaffComponent>(deadHead);
            mobState.ChangeMobState(deadHead, MobState.Dead);

            var aliveHead = entMan.SpawnEntity("MobHuman", new EntityCoordinates(grid, 1, 0));
            entMan.AddComponent<CommandStaffComponent>(aliveHead);
        });

        await server.WaitRunTicks(20);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(obr.IsTeamActive(GammaTeamId), Is.False, "Gamma ERT was called while an uncuffed command member was alive");
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound), "Round ended while an uncuffed command member was alive");
                Assert.That(alertLevel.GetLevel(station), Is.Not.EqualTo("gamma"), "Gamma alert was set while an uncuffed command member was alive");
            });
        });
    }
}
