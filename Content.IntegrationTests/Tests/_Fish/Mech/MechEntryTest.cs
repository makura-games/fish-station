using Content.Server.Mech.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mech.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish.Mech;

[TestFixture, NonParallelizable]
public sealed class MechEntryTest
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task EntryChecksInstallerAccessWithoutMutatingState(bool installerHasAccess, bool brainHasAccess)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var mechs = entMan.System<MechSystem>();
        var hands = entMan.System<SharedHandsSystem>();
        var access = entMan.System<SharedAccessSystem>();
        EntityUid installer = default;
        EntityUid mech = default;
        EntityUid brain = default;
        EntityUid heldItem = default;
        var pickedUp = false;
        var canEnter = false;
        var inserted = false;
        var originalLogCount = 0;

        await server.WaitPost(() =>
        {
            installer = entMan.SpawnEntity("MobHuman", map.GridCoords);
            mech = entMan.SpawnEntity("MechRipleyBattery", map.GridCoords);
            brain = entMan.SpawnEntity("PositronicBrain", map.GridCoords);
            heldItem = entMan.SpawnEntity("Crowbar", map.GridCoords);
            var reader = entMan.GetComponent<AccessReaderComponent>(mech);
            entMan.System<AccessReaderSystem>().TrySetAccesses((mech, reader), [["Engineering"]]);
            if (installerHasAccess)
                access.TrySetTags(installer, ["Engineering"], entMan.EnsureComponent<AccessComponent>(installer));
            if (brainHasAccess)
                access.TrySetTags(brain, ["Engineering"], entMan.EnsureComponent<AccessComponent>(brain));
            // Мозг остаётся вне рук, чтобы его тестовый Access не передавался устанавливающему.
            pickedUp = hands.TryPickupAnyHand(installer, heldItem);
            originalLogCount = reader.AccessLog.Count;
            canEnter = mechs.CanEnterMech(mech, installer, brain);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(pickedUp, Is.True);
            Assert.That(canEnter, Is.EqualTo(installerHasAccess));
            Assert.That(entMan.GetComponent<AccessReaderComponent>(mech).AccessLog.Count, Is.EqualTo(originalLogCount));
            Assert.That(entMan.GetComponent<MechComponent>(mech).PilotSlot.ContainedEntity, Is.Null);
            Assert.That(hands.IsHolding(installer, heldItem), Is.True);
        });

        await server.WaitPost(() => inserted = mechs.TryEnterMech(mech, installer, brain));
        await server.WaitAssertion(() =>
        {
            Assert.That(inserted, Is.EqualTo(installerHasAccess));
            Assert.That(entMan.GetComponent<MechComponent>(mech).PilotSlot.ContainedEntity,
                installerHasAccess ? Is.EqualTo(brain) : Is.Null);
            Assert.That(hands.IsHolding(installer, heldItem), Is.True);
        });

        await server.WaitPost(() => mechs.TryEject(mech));
        await pair.CleanReturnAsync();
    }
}
