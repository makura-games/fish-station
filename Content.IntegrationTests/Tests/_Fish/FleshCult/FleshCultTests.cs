#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Client.Chat;
using Content.Client.UserInterface.Systems.Chat;
using Content.Server._Sunrise.Chat;
using Content.Server._Sunrise.FleshCult;
using Content.Server._Sunrise.FleshCult.GameRule;
using Content.Server.Actions;
using Content.Server.Antag;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._Sunrise.CollectiveMind;
using Content.Shared._Sunrise.FleshCult;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DragDrop;
using Content.Shared.FixedPoint;
using Content.Shared.Flesh;
using Content.Shared.GameTicking;
using Content.Shared.Implants;
using Content.Shared.Mindshield;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Overlays;
using Content.Shared.Slippery;
using Content.Shared.Speech;
using Content.Shared.Weapons.Melee;
using Robust.Client.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using ServerChatSystem = Content.Server.Chat.Systems.ChatSystem;

namespace Content.IntegrationTests.Tests._Fish.FleshCult;

[TestFixture]
public sealed partial class FleshCultTests
{
    private static readonly EntProtoId FleshCultRuleId = "FleshCult";
    private static readonly EntProtoId DevourActionId = "FleshCultistDevour";

    [Test]
    public async Task FleshCultRule_DistributesRoles_WhenStartedInRound()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true,
            InLobby = true,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();
        var roleSys = server.System<RoleSystem>();
        var mindSys = server.System<MindSystem>();

        var dummies = await server.AddDummySessions(2);
        // Ждём регистрации тестовых сессий в тикере перед переключением готовности.
        await pair.RunTicksSync(5);

        await pair.SetAntagPreference("FleshCultist", true);
        await pair.SetAntagPreference("FleshCultistLeader", true);

        await server.WaitPost(() =>
        {
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
        });
        // Ждём завершения инициализации карты, спавна игроков и перехода в InRound.
        await pair.RunTicksSync(10);

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

        EntityUid ruleEnt = default;
        await server.WaitPost(() =>
        {
            ruleEnt = ticker.AddGameRule(FleshCultRuleId);
            ticker.StartGameRule(ruleEnt);
        });
        // Ждём обработки выбора антагонистов правилом FleshCult при запуске в раунде.
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent<FleshCultRuleComponent>(ruleEnt, out var ruleComp), Is.True);
            Assert.That(ruleComp!.Cultists, Is.Not.Empty, "FleshCult rule should distribute at least one cultist role when started in-round.");

            var player = pair.Player!.AttachedEntity!.Value;
            var anyHasCultistComp = dummies
                .Select(d => d.AttachedEntity)
                .Append(player)
                .Any(e => e != null && entMan.HasComponent<FleshCultistComponent>(e.Value));

            Assert.That(anyHasCultistComp, Is.True);

            var cultistUid = ruleComp.Cultists[0];
            Assert.That(mindSys.TryGetMind(cultistUid, out var mindId, out _), Is.True);
            Assert.That(roleSys.MindIsAntagonist(mindId), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FleshCult_PredatorRace_AllowedForAntagSelectionAndVirusConversion()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true,
            InLobby = false,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var antagSys = server.System<AntagSelectionSystem>();
        var mindSys = server.System<MindSystem>();
        var roleSys = server.System<RoleSystem>();
        var solutionSys = server.System<SharedSolutionContainerSystem>();

        EntityUid predator = default;
        await server.WaitPost(() =>
        {
            predator = entMan.Spawn("MobPredator", map.MapCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, predator);
            var mind = mindSys.CreateMind(pair.Player!.UserId, "Yautja Cultist");
            mindSys.TransferTo(mind, predator, true);
        });
        // Ждём репликации сущности Predator и её разума перед проверкой выбора антагонистов.
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(antagSys.IsEntityValid(predator, "SunriseFleshCultist"), Is.True,
                    "Predator (Yautja) should be valid for SunriseFleshCultist.");
                Assert.That(antagSys.IsEntityValid(predator, "SunriseFleshCultistLeader"), Is.True,
                    "Predator (Yautja) should be valid for SunriseFleshCultistLeader.");
            });
        });

        var foundBloodSolution = false;
        var carolAdded = false;
        var triggeredPendingInfection = false;

        await server.WaitPost(() =>
        {
            var bloodstream = entMan.GetComponent<BloodstreamComponent>(predator);
            foundBloodSolution = solutionSys.TryGetSolution(predator, bloodstream.BloodSolutionName, out var bloodSoln, out _);
            if (!foundBloodSolution || bloodSoln == null)
                return;

            carolAdded = solutionSys.TryAddReagent(bloodSoln.Value, "Carol", FixedPoint2.New(10));
            // TryAddReagent вызывает SolutionChangedEvent -> OnBloodstreamSolutionChanged -> TryInfectWithCarol автоматически.
            triggeredPendingInfection = entMan.TryGetComponent<PendingFleshCultistComponent>(predator, out var pending);
            if (triggeredPendingInfection)
            {
                pending!.CurrentStage = PendingFleshCultistStage.Second;
                pending.Accumulator = pending.SecondStageTimer + 1f;
            }
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(foundBloodSolution, Is.True);
            Assert.That(carolAdded, Is.True);
            Assert.That(triggeredPendingInfection, Is.True,
                "Adding Carol reagent to Predator bloodstream should trigger PendingFleshCultist infection.");
        });

        // Ждём несколько тиков цикла UpdateVirus для перевода заражённого Predator в культиста плоти.
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<FleshCultistComponent>(predator), Is.True,
                    "Predator infected with Carol should become a FleshCultist.");
                Assert.That(entMan.HasComponent<FleshAbilitiesComponent>(predator), Is.True);
                Assert.That(mindSys.TryGetMind(predator, out var mindId, out _), Is.True);
                Assert.That(roleSys.MindHasRole<FleshCultistRoleComponent>(mindId), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FleshCult_MindShield_BlocksSelectionAndCarolInfection()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var antagSys = server.System<AntagSelectionSystem>();
        var fleshSys = server.System<FleshCultSystem>();
        var implantSys = server.System<SharedSubdermalImplantSystem>();
        var mindShieldSys = server.System<MindShieldSystem>();
        var solutionSys = server.System<SharedSolutionContainerSystem>();

        EntityUid shieldedMob = default;
        EntityUid fakeShieldMob = default;

        await server.WaitPost(() =>
        {
            shieldedMob = entMan.Spawn("MobHuman", map.MapCoords);
            implantSys.AddImplant(shieldedMob, "MindShieldImplant");

            fakeShieldMob = entMan.Spawn("MobHuman", map.MapCoords);
            implantSys.AddImplant(fakeShieldMob, "FakeMindShieldImplant");
        });
        // Ждём завершения инициализации имплантов и обновления MindShieldStatusComponent.
        await pair.RunTicksSync(5);

        var foundBloodSolution = false;
        var carolAdded = false;
        var canInfect = true;
        var tryInfect = true;

        await server.WaitPost(() =>
        {
            var bloodstream = entMan.GetComponent<BloodstreamComponent>(shieldedMob);
            foundBloodSolution = solutionSys.TryGetSolution(shieldedMob, bloodstream.BloodSolutionName, out var bloodSoln, out _);
            if (!foundBloodSolution || bloodSoln == null)
                return;

            carolAdded = solutionSys.TryAddReagent(bloodSoln.Value, "Carol", FixedPoint2.New(10));
            canInfect = fleshSys.CanInfectWithCarol((shieldedMob, bloodstream), bloodSoln.Value);
            tryInfect = fleshSys.TryInfectWithCarol((shieldedMob, bloodstream), bloodSoln.Value);
        });

        await server.WaitAssertion(() =>
        {
            mindShieldSys.GetMindshieldStatus(shieldedMob, out var isShielded, out _);
            Assert.That(isShielded, Is.True);

            mindShieldSys.GetMindshieldStatus(fakeShieldMob, out var isFakeShielded, out _);
            Assert.That(isFakeShielded, Is.False);

            Assert.Multiple(() =>
            {
                Assert.That(antagSys.IsEntityValid(shieldedMob, "SunriseFleshCultist"), Is.False,
                    "MindShielded mob must not be valid for SunriseFleshCultist.");
                Assert.That(antagSys.IsEntityValid(shieldedMob, "SunriseFleshCultistLeader"), Is.False,
                    "MindShielded mob must not be valid for SunriseFleshCultistLeader.");
                Assert.That(antagSys.IsEntityValid(fakeShieldMob, "SunriseFleshCultist"), Is.True,
                    "FakeMindShield must not grant real antagonist immunity.");
            });

            Assert.That(foundBloodSolution, Is.True);
            Assert.That(carolAdded, Is.True);
            Assert.That(canInfect, Is.False, "MindShielded mob must not be infected with Carol.");
            Assert.That(tryInfect, Is.False);
            Assert.That(entMan.HasComponent<PendingFleshCultistComponent>(shieldedMob), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FleshHeart_AbsorbCorpse_DoesNotDropActionEntities()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var actionsSys = server.System<ActionsSystem>();
        var mobStateSys = server.System<MobStateSystem>();
        var xformSys = server.System<SharedTransformSystem>();
        var containerSys = server.System<SharedContainerSystem>();

        EntityUid heart = default;
        EntityUid cultist = default;
        EntityUid corpse = default;
        EntityUid? extraAction = null;
        var heartAnchored = false;
        var dragHandled = false;

        await server.WaitPost(() =>
        {
            var coords = map.GridCoords;
            heart = entMan.SpawnEntity("FleshHeart", coords);
            var heartXform = entMan.GetComponent<TransformComponent>(heart);
            if (!heartXform.Anchored)
                xformSys.AnchorEntity(heart, heartXform);
            heartAnchored = heartXform.Anchored;

            cultist = entMan.SpawnEntity("MobHuman", coords);
            entMan.EnsureComponent<FleshCultistComponent>(cultist);

            corpse = entMan.SpawnEntity("MobPredator", coords);
            actionsSys.AddAction(corpse, ref extraAction, DevourActionId);
            mobStateSys.ChangeMobState(corpse, MobState.Dead);
        });
        // Ждём завершения инициализации сердца, трупа и контейнера действий перед перетаскиванием.
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var heartComp = entMan.GetComponent<FleshHeartComponent>(heart);
            heartComp.EntryDelay = 0.05f;

            var ev = new DragDropTargetEvent(cultist, corpse);
            entMan.EventBus.RaiseLocalEvent(heart, ref ev);
            dragHandled = ev.Handled;
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(heartAnchored, Is.True, "FleshHeart must be anchored on test grid.");
            Assert.That(dragHandled, Is.True, "FleshHeart should accept drag-drop of dead Predator corpse.");
        });

        // Ждём окончания DoAfter поглощения трупа (EntryDelay = 0.05с) с запасом на тики симуляции.
        await pair.RunSeconds(1.0f);

        await server.WaitAssertion(() =>
        {
            var heartComp = entMan.GetComponent<FleshHeartComponent>(heart);
            Assert.That(heartComp.BodyContainer.ContainedEntities, Does.Contain(corpse),
                "Predator corpse should be absorbed by FleshHeart.");

            Assert.That(extraAction, Is.Not.Null, "Corpse extra action must have been created.");
            Assert.That(entMan.EntityExists(extraAction!.Value), Is.True,
                "Corpse action entity must still exist after FleshHeart absorption.");
            Assert.That(containerSys.IsEntityInContainer(extraAction.Value), Is.True,
                "Corpse extraAction must remain inside a container instead of dropping onto the floor.");

            var actionQuery = entMan.EntityQueryEnumerator<ActionComponent>();
            while (actionQuery.MoveNext(out var actionUid, out _))
            {
                Assert.That(containerSys.IsEntityInContainer(actionUid), Is.True,
                    $"Action entity {entMan.ToPrettyString(actionUid)} should not be dropped onto the floor by FleshHeart.");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FleshHeart_RejectsFourthCorpse_AfterAbsorbingThreeBodies()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var mobStateSys = server.System<MobStateSystem>();
        var xformSys = server.System<SharedTransformSystem>();

        EntityUid heart = default;
        EntityUid cultist = default;
        var corpses = new EntityUid[4];

        await server.WaitPost(() =>
        {
            var coords = map.GridCoords;
            heart = entMan.SpawnEntity("FleshHeart", coords);
            var heartXform = entMan.GetComponent<TransformComponent>(heart);
            if (!heartXform.Anchored)
                xformSys.AnchorEntity(heart, heartXform);

            var heartComp = entMan.GetComponent<FleshHeartComponent>(heart);
            heartComp.EntryDelay = 0.05f;

            cultist = entMan.SpawnEntity("MobHuman", coords);
            entMan.EnsureComponent<FleshCultistComponent>(cultist);

            for (var i = 0; i < corpses.Length; i++)
            {
                corpses[i] = entMan.SpawnEntity("MobHuman", coords);
                mobStateSys.ChangeMobState(corpses[i], MobState.Dead);
            }
        });
        // Ждём завершения инициализации сердца и четырёх трупов.
        await pair.RunTicksSync(5);

        for (var i = 0; i < 3; i++)
        {
            var index = i;
            var dragAccepted = false;
            await server.WaitPost(() =>
            {
                var ev = new DragDropTargetEvent(cultist, corpses[index]);
                entMan.EventBus.RaiseLocalEvent(heart, ref ev);
                dragAccepted = ev.Handled;
            });

            await server.WaitAssertion(() =>
            {
                Assert.That(dragAccepted, Is.True,
                    $"FleshHeart should accept corpse #{index + 1} before reaching capacity.");
            });

            // Ждём завершения DoAfter поглощения трупа (EntryDelay = 0.05с).
            await pair.RunSeconds(0.5f);
        }

        var fourthHandledWhenFull = true;
        var fourthHandledWhenActive = true;

        await server.WaitPost(() =>
        {
            var fourthEv = new DragDropTargetEvent(cultist, corpses[3]);
            entMan.EventBus.RaiseLocalEvent(heart, ref fourthEv);
            fourthHandledWhenFull = fourthEv.Handled;

            var heartComp = entMan.GetComponent<FleshHeartComponent>(heart);
            heartComp.Status = HeartStatus.Active;

            var activeEv = new DragDropTargetEvent(cultist, corpses[3]);
            entMan.EventBus.RaiseLocalEvent(heart, ref activeEv);
            fourthHandledWhenActive = activeEv.Handled;
        });

        await server.WaitAssertion(() =>
        {
            var heartComp = entMan.GetComponent<FleshHeartComponent>(heart);
            Assert.That(heartComp.BodyContainer.ContainedEntities.Count, Is.EqualTo(3),
                "FleshHeart should hold exactly 3 absorbed corpses.");
            Assert.That(fourthHandledWhenFull, Is.False,
                "FleshHeart must reject a 4th corpse once it has absorbed 3 corpses.");
            Assert.That(fourthHandledWhenActive, Is.False,
                "FleshHeart must reject corpses once it is already active.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FleshMobs_HaveNoSlip_StaminaResistance_NightVision_Flammable_And_PudgeDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var expectedNightVisionColor = Color.FromHex("#D61B1B26");

        string[] fleshMobProtos =
        [
            "MobFleshSpider",
            "MobFleshPudge",
            "MobFleshBall",
            "MobFleshBat",
            "MobFleshHugger",
            "MobFleshWorm",
            "MobSmallFleshWorm",
        ];

        var spawnedMobs = new Dictionary<string, EntityUid>();

        await server.WaitPost(() =>
        {
            foreach (var proto in fleshMobProtos)
            {
                spawnedMobs[proto] = entMan.Spawn(proto, map.MapCoords);
            }
        });
        // Ждём завершения ComponentStartup (выдача ActionToggleNightVision и др.) у заспавненных мобов.
        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            foreach (var (proto, mob) in spawnedMobs)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<NoSlipComponent>(mob), Is.True,
                        $"{proto} should have NoSlipComponent.");
                    Assert.That(entMan.TryGetComponent<StaminaResistanceComponent>(mob, out var stamRes), Is.True,
                        $"{proto} should have StaminaResistanceComponent.");
                    Assert.That(stamRes!.DamageCoefficient, Is.EqualTo(0.5f).Within(0.001f),
                        $"{proto} should have 0.5 stamina damage coefficient.");
                    Assert.That(stamRes.Worn, Is.False,
                        $"{proto} StaminaResistance should apply directly to the mob (worn: false).");

                    Assert.That(entMan.TryGetComponent<NightVisionComponent>(mob, out var nightVision), Is.True,
                        $"{proto} should have NightVisionComponent matching BloodCult constructs.");
                    Assert.That(nightVision!.Enabled, Is.False);
                    Assert.That(nightVision.Action, Is.EqualTo(new EntProtoId("ActionToggleNightVision")));
                    Assert.That(nightVision.LightingColor, Is.EqualTo(expectedNightVisionColor));
                    Assert.That(nightVision.ActionEntity, Is.Not.Null,
                        $"{proto} should receive ActionToggleNightVision on startup.");

                    Assert.That(entMan.TryGetComponent<FlammableComponent>(mob, out var flammable), Is.True,
                        $"{proto} should inherit FlammableComponent from MobFlammable.");
                    Assert.That(flammable!.FireSpread, Is.True);
                    Assert.That(flammable.CanResistFire, Is.True);

                    Assert.That(entMan.TryGetComponent<CollectiveMindComponent>(mob, out var mind), Is.True,
                        $"{proto} should have CollectiveMindComponent.");
                    Assert.That(mind!.Minds, Does.Contain(new ProtoId<CollectiveMindPrototype>("FleshCult")));
                    Assert.That(entMan.HasComponent<SpeechComponent>(mob), Is.True,
                        $"{proto} should have SpeechComponent.");
                });

                if (proto == "MobFleshPudge")
                {
                    var melee = entMan.GetComponent<MeleeWeaponComponent>(mob);
                    Assert.Multiple(() =>
                    {
                        Assert.That(melee.Damage.DamageDict["Slash"], Is.EqualTo(FixedPoint2.New(20)));
                        Assert.That(melee.Damage.DamageDict["Blunt"], Is.EqualTo(FixedPoint2.New(25)));
                        Assert.That(melee.Damage.DamageDict["Structural"], Is.EqualTo(FixedPoint2.New(35)));
                    });
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FleshMob_CollectiveMindChat_And_IgniteToDeath_WorkWithoutErrors()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true,
            InLobby = false,
        });

        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var chatSys = server.System<ServerChatSystem>();
        var flammableSys = server.System<FlammableSystem>();
        var dmgSys = server.System<DamageableSystem>();

        var mindListener = server.System<CollectiveMindListenerSystem>();

        EntityUid pudge = default;

        await server.WaitPost(() =>
        {
            mindListener.LastEvent = null;
            pudge = entMan.Spawn("MobFleshPudge", map.MapCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, pudge);
        });
        // Ждём полной репликации MobFleshPudge и его CollectiveMindComponent на клиент.
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var clientMindSys = client.System<CollectiveMindChatUpdateSystem>();
            var uiMan = client.ResolveDependency<IUserInterfaceManager>();
            var chatUi = uiMan.GetUIController<ChatUIController>();

            Assert.Multiple(() =>
            {
                Assert.That(clientMindSys.IsCollectiveMind, Is.True,
                    "Attached flesh mob player must be recognized as a collective mind speaker.");
                Assert.That(chatUi.CanSendChannels.HasFlag(ChatSelectChannel.CollectiveMind), Is.True,
                    "Client chat UI must allow CollectiveMind channel for controlled flesh mobs.");

                var (channel, _, _, mindProto) = chatUi.SplitInputContents("+п сигнал плоти");
                Assert.That(channel, Is.EqualTo(ChatSelectChannel.CollectiveMind));
                Assert.That(mindProto?.ID, Is.EqualTo("FleshCult"));
            });
        });

        await server.WaitPost(() =>
        {
            chatSys.TrySendInGameICMessage(
                pudge,
                "+п проверка связи",
                InGameICChatType.CollectiveMind,
                ChatTransmitRange.Normal,
                player: pair.Player);
        });

        await server.WaitAssertion(() =>
        {
            var capturedEvent = mindListener.LastEvent;
            Assert.That(capturedEvent, Is.Not.Null,
                "Flesh mob collective mind message should dispatch CollectiveMindSpokeEvent.");
            Assert.That(capturedEvent!.Source, Is.EqualTo(pudge));
            Assert.That(capturedEvent.CollectiveMindId, Is.EqualTo("FleshCult"));
            Assert.That(capturedEvent.Message, Is.EqualTo("Проверка связи."));
        });

        var hasFlammableAfterIgnite = false;
        var isOnFireAfterIgnite = false;

        await server.WaitPost(() =>
        {
            flammableSys.AdjustFireStacks(pudge, 10, ignite: true);
            hasFlammableAfterIgnite = entMan.TryGetComponent<FlammableComponent>(pudge, out var flammable);
            isOnFireAfterIgnite = flammable?.OnFire ?? false;

            dmgSys.TryChangeDamage(pudge, new DamageSpecifier { DamageDict = { { "Heat", FixedPoint2.New(500) } } }, true);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(hasFlammableAfterIgnite, Is.True);
            Assert.That(isOnFireAfterIgnite, Is.True, "Flesh mob should ignite cleanly.");
        });

        // Ждём репликации смерти моба и появления горящего MobFleshWorm на клиенте без падения визуализатора огня.
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent<MobStateComponent>(pudge, out var state), Is.True);
            Assert.That(state!.CurrentState, Is.EqualTo(MobState.Dead),
                "Flesh mob ignited and killed by heat damage should die without crashing client or server.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FleshCultist_RuntimeCollectiveMindSync_UpdatesClientChatPermissions()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true,
            InLobby = false,
        });
        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        EntityUid human = default;

        await server.WaitPost(() =>
        {
            human = entMan.Spawn("MobHuman", map.MapCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, human);
        });
        // Ждём репликации стартовой сущности без коллективного разума на клиент.
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var clientMindSys = client.System<CollectiveMindChatUpdateSystem>();
            var chatUi = client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>();

            Assert.That(clientMindSys.IsCollectiveMind, Is.False);
            Assert.That(chatUi.CanSendChannels.HasFlag(ChatSelectChannel.CollectiveMind), Is.False);
        });

        await server.WaitPost(() =>
        {
            entMan.EnsureComponent<FleshCultistComponent>(human);
        });
        // Ждём репликации добавления CollectiveMindComponent на клиент и обновления прав чата через ComponentInit.
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var clientMindSys = client.System<CollectiveMindChatUpdateSystem>();
            var chatUi = client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>();

            Assert.Multiple(() =>
            {
                Assert.That(clientMindSys.IsCollectiveMind, Is.True);
                Assert.That(clientMindSys.Player, Is.Not.Null);
                Assert.That(chatUi.CanSendChannels.HasFlag(ChatSelectChannel.CollectiveMind), Is.True);
            });
        });

        await server.WaitPost(() =>
        {
            entMan.RemoveComponent<FleshCultistComponent>(human);
        });
        // Ждём репликации удаления CollectiveMindComponent (при пустом Minds) на клиент.
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var clientMindSys = client.System<CollectiveMindChatUpdateSystem>();
            var chatUi = client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>();

            Assert.Multiple(() =>
            {
                Assert.That(clientMindSys.IsCollectiveMind, Is.False);
                Assert.That(chatUi.CanSendChannels.HasFlag(ChatSelectChannel.CollectiveMind), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FleshCultist_IgnitedToDeath_TransformsIntoPudge_WithoutCrashing()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
        });
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var atmosSys = server.System<AtmosphereSystem>();
        var dmgSys = server.System<DamageableSystem>();
        var flammableSys = server.System<FlammableSystem>();

        EntityUid cultist = default;
        EntityUid bystander = default;

        await server.WaitPost(() =>
        {
            var gas = new GasMixture(Atmospherics.CellVolume)
            {
                Temperature = Atmospherics.T20C,
            };
            gas.SetMoles(Gas.Oxygen, 25f);
            gas.SetMoles(Gas.Nitrogen, 75f);
            atmosSys.SetMapAtmosphere(map.MapUid, false, gas);

            cultist = entMan.Spawn("MobHuman", map.MapCoords);
            entMan.EnsureComponent<FleshCultistComponent>(cultist);

            // Вторая горящая сущность гарантирует продолжение обхода в FlammableSystem.Update.
            bystander = entMan.Spawn("MobHuman", map.MapCoords);

            // Доводим урон до предсмертного (~185), чтобы именно тик FlammableSystem.Update перевёл культиста в Dead.
            for (var i = 0; i < 12 && dmgSys.GetAllDamage(cultist).GetTotal() < FixedPoint2.New(185); i++)
            {
                dmgSys.TryChangeDamage(cultist, new DamageSpecifier { DamageDict = { { "Heat", FixedPoint2.New(25) } } }, true);
            }

            flammableSys.AdjustFireStacks(cultist, 20, ignite: true);
            flammableSys.AdjustFireStacks(bystander, 1, ignite: true);
        });

        await server.WaitAssertion(() =>
        {
            var state = entMan.GetComponent<MobStateComponent>(cultist).CurrentState;
            Assert.That(state, Is.Not.EqualTo(MobState.Dead),
                "Cultist must still be alive/critical before FlammableSystem.Update deals the lethal fire tick.");
        });

        // Ждём срабатывания 1-секундного цикла FlammableSystem.Update (смерть от огня) и отложенного превращения в MobFleshPudge.
        await pair.RunSeconds(2.5f);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(cultist), Is.True,
                "Cultist killed by fire should gib and delete after spawning MobFleshPudge.");
            Assert.That(entMan.Deleted(bystander), Is.False);

            var foundPudge = false;
            var fleshMobQuery = entMan.EntityQueryEnumerator<FleshMobComponent, MetaDataComponent>();
            while (fleshMobQuery.MoveNext(out _, out _, out var meta))
            {
                if (meta.EntityPrototype?.ID == "MobFleshPudge")
                {
                    foundPudge = true;
                    break;
                }
            }

            Assert.That(foundPudge, Is.True,
                "Burning cultist should transform into MobFleshPudge during FlammableSystem.Update without InvalidOperationException.");
        });

        await pair.CleanReturnAsync();
    }

    private sealed partial class CollectiveMindListenerSystem : EntitySystem
    {
        public CollectiveMindSpokeEvent? LastEvent;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<CollectiveMindSpokeEvent>(OnCollectiveMindSpoke);
            SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        }

        private void OnCollectiveMindSpoke(CollectiveMindSpokeEvent ev)
        {
            LastEvent = ev;
        }

        private void OnRoundRestart(RoundRestartCleanupEvent ev)
        {
            LastEvent = null;
        }
    }
}
