using Content.Server._Fish.ObrCall;
using Content.Shared._Fish.ObrCall;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.ObrCall;

[TestFixture]
[TestOf(typeof(ObrCallSystem))]
public sealed class ObrCallIntegrationTests
{
    private static readonly ProtoId<ObrTeamPrototype> AmberId = "ObrAmber";
    private static readonly ProtoId<ObrTeamPrototype> RedId = "ObrRed";
    private static readonly ProtoId<ObrTeamPrototype> GammaId = "ObrGamma";
    private static readonly ProtoId<ObrTeamPrototype> CburnId = "ObrCburn";
    private static readonly EntProtoId CentCommConsoleId = "ComputerObrCentCommConsole";
    private static readonly EntProtoId StationConsoleId = "ComputerObrStationConsole";
    private static readonly EntProtoId AmberRuleId = "FishObrShuttleAmber";
    private static readonly EntProtoId MissionRoleId = "MindRoleObrMission";
    private static readonly ProtoId<ObrCallSettingsPrototype> SettingsId = "DefaultObrCallSettings";
    private static readonly EntProtoId StandardStationId = "StandardNanotrasenStation";

    [Test]
    public async Task PrototypesExistAndGammaNotOnStationList()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            Assert.That(proto.HasIndex(AmberId), Is.True);
            Assert.That(proto.HasIndex(RedId), Is.True);
            Assert.That(proto.HasIndex(GammaId), Is.True);
            Assert.That(proto.HasIndex(CburnId), Is.True);

            Assert.That(proto.HasIndex(CentCommConsoleId), Is.True);
            Assert.That(proto.HasIndex(StationConsoleId), Is.True);
            Assert.That(proto.HasIndex(AmberRuleId), Is.True);
            Assert.That(proto.HasIndex(MissionRoleId), Is.True);
            Assert.That(proto.HasIndex(SettingsId), Is.True);
            Assert.That(proto.Index(SettingsId).ArrivalDistance, Is.EqualTo(1500f));
            Assert.That(proto.Index(SettingsId).CentCommArrivalDistance, Is.EqualTo(100f));
            Assert.That(proto.Index(SettingsId).EarliestCallTime, Is.EqualTo(TimeSpan.FromMinutes(30)));

            var gamma = proto.Index(GammaId);
            Assert.That(gamma.StationAvailable, Is.False);
            Assert.That(gamma.CentCommAvailable, Is.True);

            Assert.That(proto.Index(AmberId).StationCost, Is.EqualTo(100000));
            Assert.That(proto.Index(RedId).StationCost, Is.EqualTo(200000));
            Assert.That(proto.Index(CburnId).StationCost, Is.EqualTo(200000));

            Assert.That(proto.Index(AmberId).CryoRoleSpawners, Has.Count.EqualTo(7));
            Assert.That(proto.Index(RedId).CryoRoleSpawners, Has.Count.EqualTo(7));
            Assert.That(proto.Index(GammaId).CryoRoleSpawners, Has.Count.EqualTo(7));
            Assert.That(proto.Index(CburnId).CryoRoleSpawners, Has.Count.EqualTo(11));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StandardNanotrasenStation_HasMainStationAndCentCommLink()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var stationProto = server.ProtoMan.Index(StandardStationId);
            Assert.That(stationProto.Components.ContainsKey("MainStation"), Is.True);
            Assert.That(stationProto.Components.ContainsKey("StationCentComm"), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
