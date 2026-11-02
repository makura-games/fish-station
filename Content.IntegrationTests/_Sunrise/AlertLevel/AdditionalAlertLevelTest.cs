#nullable enable

using System.Collections.Generic;
using Content.Server._Sunrise.ExtendedAccess;
using Content.Server.AlertLevel;
using Content.Shared.AlertLevel;
using Content.Server.Communications;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._Sunrise.AlertLevel;

[TestFixture]
[TestOf(typeof(AlertLevelSystem))]
public sealed class AdditionalAlertLevelTest
{
    private static readonly ProtoId<AlertLevelPrototype> YellowLevel = "Yellow";
    private static readonly ProtoId<AlertLevelPrototype> VioletLevel = "Violet";
    private static readonly ProtoId<AlertLevelPrototype> GreenLevel = "Green";
    private static readonly ProtoId<AlertLevelPrototype> RedLevel = "Red";
    private static readonly ProtoId<AlertLevelPrototype> GammaLevel = "Gamma";
    private static readonly ProtoId<AlertLevelPrototype> DeltaLevel = "DeltaNuke";
    private static readonly ProtoId<AlertLevelPrototype> EpsilonLevel = "Epsilon";

    private static readonly List<ProtoId<AlertLevelPrototype>> DefaultAvailableLevels =
    [
        "Blue",
        "DeltaNuke",
        "Epsilon",
        "Gamma",
        "Green",
        "Red",
        "Sierra",
        "Violet",
        "Yellow",
    ];

    [Test]
    public async Task AdditionalLevelsRespectSharedCooldown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var alertLevelSystem = server.System<AlertLevelSystem>();

        EntityUid station = default;
        AlertLevelComponent alertLevel = null!;
        var blockedByPrimaryCooldown = false;
        var yellowEnabled = false;
        var forcedVioletEnabled = false;
        var yellowDisableBlockedByCooldown = false;
        var yellowDisabled = false;

        await server.WaitPost(() =>
        {
            station = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            alertLevel = entityManager.AddComponent<AlertLevelComponent>(station);
            alertLevel.AvailableAlertLevels = [.. DefaultAvailableLevels];
            alertLevel.CurrentAlertLevel = "Green";
            alertLevel.DelayedUntil = server.Timing.CurTime + TimeSpan.FromSeconds(30);

            blockedByPrimaryCooldown = !alertLevelSystem.TrySetAdditionalLevel(
                (station, alertLevel),
                "Yellow",
                true,
                playSound: false,
                announce: false);

            alertLevel.DelayedUntil = null;
            yellowEnabled = alertLevelSystem.TrySetAdditionalLevel(
                (station, alertLevel),
                "Yellow",
                true,
                playSound: false,
                announce: false);

            forcedVioletEnabled = alertLevelSystem.TrySetAdditionalLevel(
                (station, alertLevel),
                "Violet",
                true,
                playSound: false,
                announce: false,
                force: true);

            yellowDisableBlockedByCooldown = !alertLevelSystem.TrySetAdditionalLevel(
                (station, alertLevel),
                "Yellow",
                false,
                playSound: false,
                announce: false);

            alertLevel.DelayedUntil = null;
            yellowDisabled = alertLevelSystem.TrySetAdditionalLevel(
                (station, alertLevel),
                "Yellow",
                false,
                playSound: false,
                announce: false);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(blockedByPrimaryCooldown, Is.True);
                Assert.That(yellowEnabled, Is.True);
                Assert.That(forcedVioletEnabled, Is.True);
                Assert.That(yellowDisableBlockedByCooldown, Is.True);
                Assert.That(yellowDisabled, Is.True);
                Assert.That(alertLevel.CurrentAlertLevel, Is.EqualTo(new ProtoId<AlertLevelPrototype>("Green")));
                Assert.That(alertLevel.DelayedUntil, Is.Not.Null);
                Assert.That(alertLevelSystem.GetAlertLevelDelay((station, alertLevel)), Is.GreaterThan(TimeSpan.Zero));
                Assert.That(alertLevel.ActiveAdditionalLevels, Is.EquivalentTo(new[] { new ProtoId<AlertLevelPrototype>("Violet") }));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VisualPrioritySelectsHighestActiveLevel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var alertLevelSystem = server.System<AlertLevelSystem>();

        EntityUid station = default;
        AlertLevelComponent alertLevel = null!;
        var effectiveLevels = new List<string>();

        await server.WaitPost(() =>
        {
            station = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            alertLevel = entityManager.AddComponent<AlertLevelComponent>(station);
            alertLevel.AvailableAlertLevels = [.. DefaultAvailableLevels];
            alertLevel.CurrentAlertLevel = "Green";

            alertLevel.ActiveAdditionalLevels.Add("Sierra");
            effectiveLevels.Add(alertLevelSystem.TryGetVisualAlertLevel((station, alertLevel), out var level, out _)
                ? level.Id
                : string.Empty);
            alertLevel.ActiveAdditionalLevels.Remove("Sierra");

            alertLevel.ActiveAdditionalLevels.Add("Yellow");
            effectiveLevels.Add(alertLevelSystem.TryGetVisualAlertLevel((station, alertLevel), out level, out _)
                ? level.Id
                : string.Empty);

            alertLevel.CurrentAlertLevel = "Red";
            effectiveLevels.Add(alertLevelSystem.TryGetVisualAlertLevel((station, alertLevel), out level, out _)
                ? level.Id
                : string.Empty);

            alertLevel.ActiveAdditionalLevels.Add("DeltaNuke");
            effectiveLevels.Add(alertLevelSystem.TryGetVisualAlertLevel((station, alertLevel), out level, out _)
                ? level.Id
                : string.Empty);

            alertLevel.ActiveAdditionalLevels.Add("Epsilon");
            effectiveLevels.Add(alertLevelSystem.TryGetVisualAlertLevel((station, alertLevel), out level, out _)
                ? level.Id
                : string.Empty);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(effectiveLevels, Is.EqualTo(new[] { "Sierra", "Yellow", "Red", "DeltaNuke", "Epsilon" }));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdditionalLevelKeepsPrimaryEmergencyAccesses()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var accessReaderSystem = server.System<AccessReaderSystem>();

        AccessReaderComponent accessReader = null!;
        var securityAllowed = false;
        var atmosphericsAllowed = false;
        var engineeringAllowed = false;

        await server.WaitPost(() =>
        {
            var reader = entityManager.SpawnEntity("DoorElectronicsLawyer", MapCoordinates.Nullspace);
            accessReader = entityManager.GetComponent<AccessReaderComponent>(reader);

            accessReaderSystem.UpdateAccess(
                (reader, accessReader),
                "Red",
                new[] { "Red", "Yellow" },
                new HashSet<ProtoId<AccessGroupPrototype>>
                {
                    new("YellowAlertAccesses"),
                });

            securityAllowed = accessReaderSystem.IsAccessAllowedByExtendedAccess(
                new HashSet<ProtoId<AccessLevelPrototype>> { new("Security") },
                accessReader);
            atmosphericsAllowed = accessReaderSystem.IsAccessAllowedByExtendedAccess(
                new HashSet<ProtoId<AccessLevelPrototype>> { new("Atmospherics") },
                accessReader);
            engineeringAllowed = accessReaderSystem.IsAccessAllowedByExtendedAccess(
                new HashSet<ProtoId<AccessLevelPrototype>> { new("Engineering") },
                accessReader);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(accessReader.Group, Is.EqualTo(new ProtoId<AccessGroupPrototype>("RedAlertAccesses")));
                Assert.That(accessReader.AdditionalGroups,
                    Does.Contain(new ProtoId<AccessGroupPrototype>("YellowAlertAccesses")));
                Assert.That(securityAllowed, Is.True);
                Assert.That(atmosphericsAllowed, Is.True);
                Assert.That(engineeringAllowed, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DisablingAdditionalLevelRevokesAccessImmediately()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var accessReaderSystem = server.System<AccessReaderSystem>();
        var alertLevelSystem = server.System<AlertLevelSystem>();
        _ = server.System<ExtendedAccessSystem>();

        await pair.CreateTestMap();
        var testMap = pair.TestMap ?? throw new InvalidOperationException("Test map was not created.");
        AccessReaderComponent accessReader = null!;
        AlertLevelComponent alert = null!;
        var yellowEnabled = false;
        var yellowDisabled = false;

        await server.WaitPost(() =>
        {
            var station = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            alert = entityManager.AddComponent<AlertLevelComponent>(station);
            alert.AvailableAlertLevels = [.. DefaultAvailableLevels];
            alert.CurrentAlertLevel = "Green";

            var stationMember = entityManager.EnsureComponent<StationMemberComponent>(testMap.Grid);
            stationMember.Station = station;

            var reader = entityManager.SpawnEntity("DoorElectronicsLawyer", testMap.GridCoords);
            accessReader = entityManager.GetComponent<AccessReaderComponent>(reader);
            yellowEnabled = alertLevelSystem.TrySetAdditionalLevel(
                (station, alert),
                "Yellow",
                true,
                playSound: false,
                announce: false);

            // Имитируем уже завершившуюся выдачу, не ожидая минутный таймер в тесте.
            accessReaderSystem.UpdateAccess(
                (reader, accessReader),
                alert.CurrentAlertLevel.Id,
                new[] { "Green", "Yellow" },
                new HashSet<ProtoId<AccessGroupPrototype>>
                {
                    new("YellowAlertAccesses"),
                });

            alert.DelayedUntil = null;
            yellowDisabled = alertLevelSystem.TrySetAdditionalLevel(
                (station, alert),
                "Yellow",
                false,
                playSound: false,
                announce: false);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(yellowEnabled, Is.True);
                Assert.That(yellowDisabled, Is.True);
                Assert.That(alert.ActiveAdditionalLevels, Does.Not.Contain(new ProtoId<AlertLevelPrototype>("Yellow")));
                Assert.That(accessReader.AdditionalGroups,
                    Does.Not.Contain(new ProtoId<AccessGroupPrototype>("YellowAlertAccesses")));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ImmediateAdditionalLevelDoesNotGrantPendingLevelAccess()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var alertLevelSystem = server.System<AlertLevelSystem>();
        _ = server.System<ExtendedAccessSystem>();

        await pair.CreateTestMap();
        var testMap = pair.TestMap ?? throw new InvalidOperationException("Test map was not created.");
        AccessReaderComponent accessReader = null!;
        AlertLevelComponent alert = null!;
        EntityUid station = default;
        var yellowEnabled = false;
        var deltaEnabled = false;

        await server.WaitPost(() =>
        {
            station = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            alert = entityManager.AddComponent<AlertLevelComponent>(station);
            alert.AvailableAlertLevels = [.. DefaultAvailableLevels];
            alert.CurrentAlertLevel = "Green";

            var stationMember = entityManager.EnsureComponent<StationMemberComponent>(testMap.Grid);
            stationMember.Station = station;

            var reader = entityManager.SpawnEntity("DoorElectronicsLawyer", testMap.GridCoords);
            accessReader = entityManager.GetComponent<AccessReaderComponent>(reader);

            yellowEnabled = alertLevelSystem.TrySetAdditionalLevel(
                (station, alert),
                "Yellow",
                true,
                playSound: false,
                announce: false,
                force: true);
            deltaEnabled = alertLevelSystem.TrySetAdditionalLevel(
                (station, alert),
                "DeltaNuke",
                true,
                playSound: false,
                announce: false,
                force: true);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(yellowEnabled, Is.True);
                Assert.That(deltaEnabled, Is.True);
                Assert.That(alert.ActiveAdditionalLevels, Is.EquivalentTo(new[]
                {
                    new ProtoId<AlertLevelPrototype>("Yellow"),
                    new ProtoId<AlertLevelPrototype>("DeltaNuke"),
                }));
                Assert.That(accessReader.AdditionalGroups,
                    Does.Contain(new ProtoId<AccessGroupPrototype>("AllAccess")));
                Assert.That(accessReader.AdditionalGroups,
                    Does.Not.Contain(new ProtoId<AccessGroupPrototype>("YellowAlertAccesses")));
            });
        });

        await server.WaitPost(() =>
        {
            alertLevelSystem.TrySetAdditionalLevel(
                (station, alert),
                "DeltaNuke",
                false,
                playSound: false,
                announce: false,
                force: true);
            alertLevelSystem.TrySetAdditionalLevel(
                (station, alert),
                "Yellow",
                false,
                playSound: false,
                announce: false,
                force: true);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DenyTagsOverrideAlertLevelAccessGroups()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var accessReaderSystem = server.System<AccessReaderSystem>();
        AccessReaderComponent accessReader = null!;
        var allowed = true;

        await server.WaitPost(() =>
        {
            var reader = entityManager.SpawnEntity("DoorElectronicsLawyer", MapCoordinates.Nullspace);
            accessReader = entityManager.GetComponent<AccessReaderComponent>(reader);
            accessReaderSystem.UpdateAccess((reader, accessReader), "Red");
            accessReaderSystem.SetDenyTags(
                (reader, accessReader),
                [new ProtoId<AccessLevelPrototype>("Security")]);

            allowed = accessReaderSystem.IsAccessAllowedByExtendedAccess(
                new HashSet<ProtoId<AccessLevelPrototype>> { new("Security") },
                accessReader);
        });

        await server.WaitAssertion(() => Assert.That(allowed, Is.False));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdditionalAccessGroupDoesNotReplacePrimaryGroup()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var accessReaderSystem = server.System<AccessReaderSystem>();
        AccessReaderComponent accessReader = null!;

        await server.WaitPost(() =>
        {
            var reader = entityManager.SpawnEntity("DoorElectronicsLawyer", MapCoordinates.Nullspace);
            accessReader = entityManager.GetComponent<AccessReaderComponent>(reader);
            accessReaderSystem.UpdateAccess(
                (reader, accessReader),
                "Red",
                new[] { "Red", "Yellow" },
                new HashSet<ProtoId<AccessGroupPrototype>>
                {
                    new("YellowAlertAccesses"),
                });
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(accessReader.Group, Is.EqualTo(new ProtoId<AccessGroupPrototype>("RedAlertAccesses")));
                Assert.That(accessReader.AdditionalGroups,
                    Does.Contain(new ProtoId<AccessGroupPrototype>("YellowAlertAccesses")));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConsoleAlertLevelAllowlistIsEnforced()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypeManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var engineeringConsole = new CommunicationsConsoleComponent
            {
                AllowedAlertLevels = ["Yellow"],
            };
            var disabledConsole = new CommunicationsConsoleComponent
            {
                AllowedAlertLevels = [],
            };
            var centCommConsole = new CommunicationsConsoleComponent
            {
                AllowedAlertLevels = ["Green", "Blue", "Violet", "Yellow", "Red", "Gamma", "DeltaNuke"],
                ForceAlertLevelChanges = true,
            };

            var yellow = prototypeManager.Index(YellowLevel);
            var violet = prototypeManager.Index(VioletLevel);
            var green = prototypeManager.Index(GreenLevel);
            var red = prototypeManager.Index(RedLevel);
            var gamma = prototypeManager.Index(GammaLevel);
            var delta = prototypeManager.Index(DeltaLevel);
            var epsilon = prototypeManager.Index(EpsilonLevel);

            Assert.Multiple(() =>
            {
                Assert.That(CommunicationsConsoleSystem.IsAlertLevelAllowed(
                    engineeringConsole,
                    "Yellow",
                    yellow), Is.True);
                Assert.That(CommunicationsConsoleSystem.IsAlertLevelAllowed(
                    engineeringConsole,
                    "Violet",
                    violet), Is.False);
                Assert.That(CommunicationsConsoleSystem.IsAlertLevelAllowed(
                    engineeringConsole,
                    "Green",
                    green), Is.False);
                Assert.That(CommunicationsConsoleSystem.IsAlertLevelAllowed(
                    disabledConsole,
                    "Red",
                    red), Is.False);
                Assert.That(CommunicationsConsoleSystem.IsAlertLevelAllowed(
                    centCommConsole,
                    "Gamma",
                    gamma), Is.True);
                Assert.That(gamma.IsAdditional, Is.False);
                Assert.That(CommunicationsConsoleSystem.IsAlertLevelAllowed(
                    centCommConsole,
                    "DeltaNuke",
                    delta), Is.True);
                Assert.That(delta.IsAdditional, Is.True);
                Assert.That(CommunicationsConsoleSystem.IsAlertLevelAllowed(
                    centCommConsole,
                    "Epsilon",
                    epsilon), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CentCommConsoleCanSelectRemoteAlertStation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var alertLevelSystem = server.System<AlertLevelSystem>();
        var communicationsSystem = server.System<CommunicationsConsoleSystem>();

        EntityUid console = default;
        CommunicationsConsoleComponent consoleComponent = null!;
        EntityUid user = default;
        EntityUid validStation = default;
        EntityUid invalidStation = default;
        var validStationSelected = false;
        var remoteAlertLevelSet = false;
        var primaryCooldownStarted = false;
        var additionalAlertLevelSet = false;
        var additionalCooldownStarted = false;
        var repeatedAdditionalLevelRejected = false;
        var rejectedChangeKeptCooldownClear = false;
        var invalidStationRejected = false;
        var disabledSelectionRejected = false;

        await server.WaitPost(() =>
        {
            console = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            consoleComponent = entityManager.AddComponent<CommunicationsConsoleComponent>(console);
            consoleComponent.CanSelectAlertStation = true;
            consoleComponent.ForceAlertLevelChanges = true;
            consoleComponent.AllowedAlertLevels = ["Gamma", "DeltaNuke"];
            user = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);

            validStation = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            entityManager.AddComponent<StationDataComponent>(validStation);
            var validAlert = entityManager.AddComponent<AlertLevelComponent>(validStation);
            validAlert.AvailableAlertLevels = [.. DefaultAvailableLevels];
            validAlert.CurrentAlertLevel = "Green";

            invalidStation = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            entityManager.AddComponent<StationDataComponent>(invalidStation);

            validStationSelected = communicationsSystem.TrySelectAlertStation(
                (console, consoleComponent),
                validStation,
                user);
            remoteAlertLevelSet = communicationsSystem.TrySetPrimaryAlertLevel(
                (console, consoleComponent),
                "Gamma",
                user);
            primaryCooldownStarted = alertLevelSystem.GetAlertLevelDelay((validStation, validAlert)) > TimeSpan.Zero;

            validAlert.DelayedUntil = null;
            additionalAlertLevelSet = communicationsSystem.TrySetAdditionalAlertLevel(
                (console, consoleComponent),
                "DeltaNuke",
                true,
                user);
            additionalCooldownStarted = alertLevelSystem.GetAlertLevelDelay((validStation, validAlert)) > TimeSpan.Zero;

            validAlert.DelayedUntil = null;
            repeatedAdditionalLevelRejected = !communicationsSystem.TrySetAdditionalAlertLevel(
                (console, consoleComponent),
                "DeltaNuke",
                true,
                user);
            rejectedChangeKeptCooldownClear = alertLevelSystem.GetAlertLevelDelay((validStation, validAlert)) <= TimeSpan.Zero;

            invalidStationRejected = !communicationsSystem.TrySelectAlertStation(
                (console, consoleComponent),
                invalidStation,
                user);

            consoleComponent.CanSelectAlertStation = false;
            disabledSelectionRejected = !communicationsSystem.TrySelectAlertStation(
                (console, consoleComponent),
                validStation,
                user);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(validStationSelected, Is.True);
                Assert.That(remoteAlertLevelSet, Is.True);
                Assert.That(entityManager.GetComponent<AlertLevelComponent>(validStation).CurrentAlertLevel,
                    Is.EqualTo(new ProtoId<AlertLevelPrototype>("Gamma")));
                Assert.That(primaryCooldownStarted, Is.True);
                Assert.That(additionalAlertLevelSet, Is.True);
                Assert.That(additionalCooldownStarted, Is.True);
                Assert.That(repeatedAdditionalLevelRejected, Is.True);
                Assert.That(rejectedChangeKeptCooldownClear, Is.True);
                Assert.That(invalidStationRejected, Is.True);
                Assert.That(disabledSelectionRejected, Is.True);
                Assert.That(consoleComponent.SelectedAlertStation, Is.EqualTo(validStation));
            });
        });

        await pair.CleanReturnAsync();
    }
}
