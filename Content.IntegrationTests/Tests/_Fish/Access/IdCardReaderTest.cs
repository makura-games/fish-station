using Content.IntegrationTests.Utility;
using Content.Server._Fish.Access;
using Content.Server.DeviceLinking.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Shared._Fish.Access;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceNetwork;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Fish.Access;

[TestFixture]
[TestOf(typeof(IdCardReaderSystem))]
public sealed class IdCardReaderTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: FishIdCardReaderTestPulseSink
          components:
          - type: DeviceLinkSink
            ports:
            - InputA
          - type: LogicGate

        - type: entity
          id: FishIdCardReaderTestStateSink
          parent: BaseLogicItem
          components:
          - type: DeviceLinkSink
            ports:
            - InputA
          - type: LogicGate

        - type: entity
          id: FishIdCardReaderTestDoorSource
          parent: BaseLogicItem
          components:
          - type: DeviceLinkSource
            ports:
            - DoorStatus
            lastSignals:
              DoorStatus: false

        - type: entity
          id: FishIdCardReaderTestTwoAuthorizations
          parent: FishIdCardReaderTwoAuthorizations
          components:
          - type: IdCardReader
            closeRetryDelay: 10

        - type: entity
          id: FishIdCardReaderTestHeadOfSecurityTwoAuthorizations
          parent: FishIdCardReaderTwoAuthorizations
          components:
          - type: AccessReader
            access: [["HeadOfSecurity"]]

        - type: entity
          id: FishIdCardReaderTestCaptainTwoAuthorizations
          parent: FishIdCardReaderTwoAuthorizations
          components:
          - type: AccessReader
            access: [["Captain"]]
        """;

    [Test]
    public async Task ReaderSignals_WhenCardsInsertedAndRemoved_ShouldMatchAccess()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var deviceLink = server.System<DeviceLinkSystem>();
        var itemSlots = server.System<ItemSlotsSystem>();

        EntityUid reader = default;
        EntityUid insertedSink = default;
        EntityUid removedSink = default;
        EntityUid grantedSink = default;
        EntityUid passengerInsertedSink = default;
        EntityUid passengerGrantedSink = default;
        EntityUid captainCard = default;
        EntityUid passengerCard = default;
        var captainInserted = false;
        var captainInsertedSignal = false;
        var captainEjected = false;
        var captainRemovedSignal = false;
        EntityUid? ejectedCard = null;
        var passengerInserted = false;
        var passengerInsertedSignal = false;

        await server.WaitPost(() =>
        {
            reader = server.EntMan.SpawnEntity("FishIdCardReaderCaptain", MapCoordinates.Nullspace);
            insertedSink = server.EntMan.SpawnEntity("FishIdCardReaderTestPulseSink", MapCoordinates.Nullspace);
            removedSink = server.EntMan.SpawnEntity("FishIdCardReaderTestPulseSink", MapCoordinates.Nullspace);
            grantedSink = server.EntMan.SpawnEntity("FishIdCardReaderTestStateSink", MapCoordinates.Nullspace);
            passengerInsertedSink = server.EntMan.SpawnEntity("FishIdCardReaderTestPulseSink", MapCoordinates.Nullspace);
            passengerGrantedSink = server.EntMan.SpawnEntity("FishIdCardReaderTestStateSink", MapCoordinates.Nullspace);
            captainCard = server.EntMan.SpawnEntity("CaptainIDCard", MapCoordinates.Nullspace);
            passengerCard = server.EntMan.SpawnEntity("PassengerIDCard", MapCoordinates.Nullspace);

            var sourceComponent = server.EntMan.GetComponent<DeviceLinkSourceComponent>(reader);
            deviceLink.SaveLinks(
                null,
                reader,
                insertedSink,
                [("FishIdCardInserted", "InputA")],
                sourceComponent,
                server.EntMan.GetComponent<DeviceLinkSinkComponent>(insertedSink));
            deviceLink.SaveLinks(
                null,
                reader,
                removedSink,
                [("FishIdCardRemoved", "InputA")],
                sourceComponent,
                server.EntMan.GetComponent<DeviceLinkSinkComponent>(removedSink));
            deviceLink.SaveLinks(
                null,
                reader,
                grantedSink,
                [("FishIdCardAccessGranted", "InputA")],
                sourceComponent,
                server.EntMan.GetComponent<DeviceLinkSinkComponent>(grantedSink));

            captainInserted = itemSlots.TryInsert(
                reader,
                IdCardReaderComponent.CardSlotId,
                captainCard,
                null);
            captainInsertedSignal = server.EntMan.GetComponent<LogicGateComponent>(insertedSink).StateA != SignalState.Low;
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(captainInserted, Is.True);
            Assert.That(captainInsertedSignal, Is.True);
            Assert.That(server.EntMan.GetComponent<LogicGateComponent>(grantedSink).StateA, Is.EqualTo(SignalState.High));
        });

        await server.WaitPost(() =>
        {
            captainEjected = itemSlots.TryEject(
                reader,
                IdCardReaderComponent.CardSlotId,
                null,
                out ejectedCard);
            captainRemovedSignal = server.EntMan.GetComponent<LogicGateComponent>(removedSink).StateA != SignalState.Low;
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(captainEjected, Is.True);
            Assert.That(ejectedCard, Is.EqualTo(captainCard));
            Assert.That(captainRemovedSignal, Is.True);
            Assert.That(server.EntMan.GetComponent<LogicGateComponent>(grantedSink).StateA, Is.EqualTo(SignalState.Low));
        });

        await server.WaitPost(() =>
        {
            var sourceComponent = server.EntMan.GetComponent<DeviceLinkSourceComponent>(reader);
            deviceLink.SaveLinks(
                null,
                reader,
                passengerInsertedSink,
                [("FishIdCardInserted", "InputA")],
                sourceComponent,
                server.EntMan.GetComponent<DeviceLinkSinkComponent>(passengerInsertedSink));
            deviceLink.SaveLinks(
                null,
                reader,
                passengerGrantedSink,
                [("FishIdCardAccessGranted", "InputA")],
                sourceComponent,
                server.EntMan.GetComponent<DeviceLinkSinkComponent>(passengerGrantedSink));

            passengerInserted = itemSlots.TryInsert(
                reader,
                IdCardReaderComponent.CardSlotId,
                passengerCard,
                null);
            passengerInsertedSignal = server.EntMan.GetComponent<LogicGateComponent>(passengerInsertedSink).StateA != SignalState.Low;
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(passengerInserted, Is.True);
            Assert.That(passengerInsertedSignal, Is.True);
            Assert.That(server.EntMan.GetComponent<LogicGateComponent>(passengerGrantedSink).StateA, Is.EqualTo(SignalState.Low));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TwoReaders_WhenBothAuthorized_ShouldScheduleCloseAndRetryUntilDoorCloses()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var deviceLink = server.System<DeviceLinkSystem>();
        var itemSlots = server.System<ItemSlotsSystem>();
        var readerSystem = server.System<IdCardReaderSystem>();

        EntityUid master = default;
        EntityUid secondary = default;
        EntityUid grantedSink = default;
        EntityUid closeSink = default;
        EntityUid doorSource = default;
        EntityUid firstCard = default;
        EntityUid secondCard = default;
        var firstInserted = false;
        var secondInserted = false;

        await server.WaitPost(() =>
        {
            master = server.EntMan.SpawnEntity("FishIdCardReaderTestTwoAuthorizations", MapCoordinates.Nullspace);
            secondary = server.EntMan.SpawnEntity("FishIdCardReader", MapCoordinates.Nullspace);
            grantedSink = server.EntMan.SpawnEntity("FishIdCardReaderTestStateSink", MapCoordinates.Nullspace);
            closeSink = server.EntMan.SpawnEntity("FishIdCardReaderTestPulseSink", MapCoordinates.Nullspace);
            doorSource = server.EntMan.SpawnEntity("FishIdCardReaderTestDoorSource", MapCoordinates.Nullspace);
            firstCard = server.EntMan.SpawnEntity("PassengerIDCard", MapCoordinates.Nullspace);
            secondCard = server.EntMan.SpawnEntity("PassengerIDCard", MapCoordinates.Nullspace);

            var masterSource = server.EntMan.GetComponent<DeviceLinkSourceComponent>(master);
            var secondarySource = server.EntMan.GetComponent<DeviceLinkSourceComponent>(secondary);
            var doorStatusSource = server.EntMan.GetComponent<DeviceLinkSourceComponent>(doorSource);
            var masterSink = server.EntMan.GetComponent<DeviceLinkSinkComponent>(master);

            deviceLink.SaveLinks(
                null,
                secondary,
                master,
                [("FishIdCardLocalAuthorization", "FishIdCardReaderAuthorization")],
                secondarySource,
                masterSink);
            deviceLink.SaveLinks(
                null,
                master,
                grantedSink,
                [("FishIdCardAccessGranted", "InputA")],
                masterSource,
                server.EntMan.GetComponent<DeviceLinkSinkComponent>(grantedSink));
            deviceLink.SaveLinks(
                null,
                master,
                closeSink,
                [("FishIdCardReaderCloseRequest", "InputA")],
                masterSource,
                server.EntMan.GetComponent<DeviceLinkSinkComponent>(closeSink));
            deviceLink.SaveLinks(
                null,
                doorSource,
                master,
                [("DoorStatus", "FishIdCardReaderDoorStatus")],
                doorStatusSource,
                masterSink);

            deviceLink.SendSignal(doorSource, "DoorStatus", true, doorStatusSource);
            firstInserted = itemSlots.TryInsert(
                master,
                IdCardReaderComponent.CardSlotId,
                firstCard,
                null);
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(firstInserted, Is.True);
            Assert.That(server.EntMan.GetComponent<IdCardReaderComponent>(master).AuthorizationOutput, Is.False);
            Assert.That(server.EntMan.GetComponent<LogicGateComponent>(grantedSink).StateA, Is.EqualTo(SignalState.Low));
        });

        await server.WaitPost(() =>
        {
            secondInserted = itemSlots.TryInsert(
                secondary,
                IdCardReaderComponent.CardSlotId,
                secondCard,
                null);
        });

        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(secondInserted, Is.True);
            var masterReader = server.EntMan.GetComponent<IdCardReaderComponent>(master);
            var secondaryReader = server.EntMan.GetComponent<IdCardReaderComponent>(secondary);
            Assert.That(masterReader.LocalAuthorization, Is.True);
            Assert.That(secondaryReader.LocalAuthorization, Is.True);
            Assert.That(secondaryReader.AuthorizationOutput, Is.True);
            Assert.That(masterReader.RemoteAuthorizations, Does.Contain(secondary));
            Assert.That(masterReader.AuthorizationOutput, Is.True);
            Assert.That(masterReader.OpenDoors, Does.Contain(doorSource));
            Assert.That(server.EntMan.GetComponent<LogicGateComponent>(grantedSink).StateA, Is.EqualTo(SignalState.High));

            var active = server.EntMan.GetComponent<ActiveIdCardReaderComponent>(master);
            Assert.That(active.NextCloseAttempt, Is.Not.Null);
            Assert.That(active.NextCloseAttempt, Is.GreaterThan(server.Timing.CurTime));
            Assert.That(active.NextCloseAttempt, Is.LessThanOrEqualTo(server.Timing.CurTime + TimeSpan.FromSeconds(4)));
        });

        await server.WaitPost(() =>
        {
            server.EntMan.GetComponent<ActiveIdCardReaderComponent>(master).NextCloseAttempt = server.Timing.CurTime;
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var active = server.EntMan.GetComponent<ActiveIdCardReaderComponent>(master);
            Assert.That(active.NextCloseAttempt, Is.GreaterThan(server.Timing.CurTime));
        });

        var closeRequested = false;
        var closeSignalReceived = false;
        TimeSpan? nextRetry = null;
        TimeSpan retryCheckedAt = default;

        await server.WaitPost(() =>
        {
            var reader = server.EntMan.GetComponent<IdCardReaderComponent>(master);
            var active = server.EntMan.GetComponent<ActiveIdCardReaderComponent>(master);
            active.NextCloseAttempt = server.Timing.CurTime;

            closeRequested = readerSystem.TryRequestClose((master, reader), active);
            closeSignalReceived = server.EntMan.GetComponent<LogicGateComponent>(closeSink).StateA != SignalState.Low;
            nextRetry = active.NextCloseAttempt;
            retryCheckedAt = server.Timing.CurTime;
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(closeRequested, Is.True);
            Assert.That(closeSignalReceived, Is.True);
            Assert.That(nextRetry, Is.GreaterThan(retryCheckedAt));
        });

        await server.WaitPost(() => deviceLink.SendSignal(doorSource, "DoorStatus", false));
        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<IdCardReaderComponent>(master).OpenDoors, Is.Empty);
            Assert.That(server.EntMan.HasComponent<ActiveIdCardReaderComponent>(master), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TwoReaders_WithHeadOfSecurityAndCaptainAccess_ShouldAuthorize()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var deviceLink = server.System<DeviceLinkSystem>();
        var itemSlots = server.System<ItemSlotsSystem>();

        EntityUid master = default;
        EntityUid secondary = default;
        EntityUid headOfSecurityCard = default;
        EntityUid captainCard = default;
        EntityUid shutter = default;

        await server.WaitPost(() =>
        {
            master = server.EntMan.SpawnEntity(
                "FishIdCardReaderTestHeadOfSecurityTwoAuthorizations",
                MapCoordinates.Nullspace);
            secondary = server.EntMan.SpawnEntity(
                "FishIdCardReaderTestCaptainTwoAuthorizations",
                MapCoordinates.Nullspace);
            headOfSecurityCard = server.EntMan.SpawnEntity("HoSIDCard", MapCoordinates.Nullspace);
            captainCard = server.EntMan.SpawnEntity("CaptainIDCard", MapCoordinates.Nullspace);
            shutter = server.EntMan.SpawnEntity("ShuttersNormal", MapCoordinates.Nullspace);

            deviceLink.SaveLinks(
                null,
                secondary,
                master,
                [("FishIdCardLocalAuthorization", "FishIdCardReaderAuthorization")],
                server.EntMan.GetComponent<DeviceLinkSourceComponent>(secondary),
                server.EntMan.GetComponent<DeviceLinkSinkComponent>(master));
            deviceLink.SaveLinks(
                null,
                master,
                shutter,
                [("FishIdCardAccessGranted", "Open")],
                server.EntMan.GetComponent<DeviceLinkSourceComponent>(master),
                server.EntMan.GetComponent<DeviceLinkSinkComponent>(shutter));
        });

        await pair.RunTicksSync(2);

        await server.WaitPost(() =>
        {
            itemSlots.TryInsert(master, IdCardReaderComponent.CardSlotId, headOfSecurityCard, null);
            itemSlots.TryInsert(secondary, IdCardReaderComponent.CardSlotId, captainCard, null);
        });

        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            var masterReader = server.EntMan.GetComponent<IdCardReaderComponent>(master);
            var secondaryReader = server.EntMan.GetComponent<IdCardReaderComponent>(secondary);
            Assert.That(masterReader.LocalAuthorization, Is.True);
            Assert.That(secondaryReader.LocalAuthorization, Is.True);
            Assert.That(secondaryReader.AuthorizationOutput, Is.False);
            Assert.That(masterReader.RemoteAuthorizations, Does.Contain(secondary));
            Assert.That(masterReader.AuthorizationOutput, Is.True);
            Assert.That(server.EntMan.GetComponent<DoorComponent>(shutter).State, Is.Not.EqualTo(DoorState.Closed));
        });

        await pair.CleanReturnAsync();
    }
}
