using Content.Server.Mech.Systems;
using Content.Shared._Fish.Mech.Events;
using Content.Shared.Shuttles.BUIStates;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish.Mech;

[TestFixture, NonParallelizable]
public sealed class MechMassScannerTest
{
    [TestCase("MechClarkeBattery", false)]
    [TestCase("MechClarkeBattery", true)]
    [TestCase("MechMiningPod", false)]
    [TestCase("MechMiningPod", true)]
    public async Task ScannerClosesWhenPilotLeaves(string prototype, bool breakMech)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var mechs = entMan.System<MechSystem>();
        var ui = entMan.System<UserInterfaceSystem>();
        EntityUid mech = default;
        EntityUid pilot = default;
        var inserted = false;
        var action = new MechOpenMassScannerEvent();

        await server.WaitPost(() =>
        {
            mech = entMan.SpawnEntity(prototype, map.GridCoords);
            pilot = entMan.SpawnEntity("PositronicBrain", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, pilot);
            inserted = mechs.TryInsert(mech, pilot);
            action.Performer = pilot;
            entMan.EventBus.RaiseLocalEvent(mech, action);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(inserted, Is.True);
            Assert.That(action.Handled, Is.True);
            Assert.That(ui.IsUiOpen(mech, RadarConsoleUiKey.Key, pilot), Is.True);
        });

        await server.WaitPost(() =>
        {
            if (breakMech)
                mechs.BreakMech(mech);
            else
                mechs.TryEject(mech);
        });

        await server.WaitAssertion(() =>
            Assert.That(ui.IsUiOpen(mech, RadarConsoleUiKey.Key, pilot), Is.False));

        await server.WaitPost(() => server.PlayerMan.SetAttachedEntity(pair.Player!, null));
        await pair.CleanReturnAsync();
    }
}
