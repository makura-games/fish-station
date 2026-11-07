using System;
using System.Collections.Generic;
using Content.Server._Fish.Xenobiology;
using Content.Server.NPC.HTN;
using Content.Shared._Fish.Xenobiology;
using Content.Shared.Atmos.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Temperature.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Xenobiology;

[TestFixture, NonParallelizable]
public sealed class FishXenobiologyReactionTest
{
    [Test]
    public async Task ColdReactionWaitsBeforeCooling()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            EntityUid extract = default;
            await server.WaitPost(() =>
            {
                lab.SetTemperature(293.15f);
                extract = server.EntMan.SpawnEntity("FishXenoExtractDarkBlue", lab.Coordinates);
                var solutions = server.System<SharedSolutionContainerSystem>();
                if (solutions.TryGetSolution(extract, "extract", out var solution, out _))
                    solutions.TryAddReagent(solution.Value, "Plasma", FixedPoint2.New(10));
            });

            await lab.Pair.RunSeconds(2);
            await server.WaitAssertion(() =>
                Assert.That(server.EntMan.GetComponent<TemperatureComponent>(lab.Slime).Temperature,
                    Is.GreaterThan(270), "Реакция должна дать время отойти от экстракта."));

            await lab.Pair.RunSeconds(1.3f);
            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.GetComponent<TemperatureComponent>(lab.Slime).Temperature,
                    Is.LessThan(220), "После задержки должен сработать холодовой импульс.");
                Assert.That(server.EntMan.GetComponent<FishXenobiologyExtractComponent>(extract).Used, Is.True);
            });
        });
    }

    [TestCase("Orange")]
    [TestCase("DarkBlue")]
    [TestCase("Sepia")]
    public async Task LocalPulseRespectsDelayWallsAndRange(string breed)
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            var entMan = server.EntMan;
            EntityUid near = default;
            EntityUid blocked = default;
            EntityUid far = default;
            await server.WaitPost(() =>
            {
                var transform = server.System<SharedTransformSystem>();
                transform.SetCoordinates(lab.User, Position(lab, 10, 10));
                transform.SetCoordinates(lab.Slime, Position(lab, 10, 11));
                near = entMan.SpawnEntity("MobHuman", Position(lab, 3.25f, 1.5f));
                blocked = entMan.SpawnEntity("MobHuman", Position(lab, 2.25f, 3.4f));
                far = entMan.SpawnEntity("MobHuman", Position(lab, 8, 1.5f));
                foreach (var target in new[] { near, blocked, far })
                    entMan.GetComponent<FlammableComponent>(target).CanExtinguish = false;
                entMan.SpawnEntity("WallSolid", Position(lab, 2.5f, 2.5f));
                Activate(lab, breed);
            });

            bool Affected(EntityUid uid) => breed switch
            {
                "Orange" => entMan.GetComponent<FlammableComponent>(uid).OnFire,
                "DarkBlue" => entMan.GetComponent<TemperatureComponent>(uid).Temperature < 260f,
                _ => entMan.HasComponent<StunnedComponent>(uid),
            };

            await lab.Pair.RunSeconds(2);
            await server.WaitAssertion(() => Assert.That(Affected(near), Is.False));
            await lab.Pair.RunSeconds(1.2f);
            await server.WaitAssertion(() =>
            {
                Assert.That(Affected(near), Is.True, "Ближняя открытая цель должна получить эффект.");
                Assert.That(Affected(blocked), Is.False, "Стена должна останавливать локальный импульс.");
                Assert.That(Affected(far), Is.False, "Цель вне радиуса не должна получать эффект.");
            });
            if (breed == "Sepia")
            {
                await lab.Pair.RunSeconds(3.2f);
                await server.WaitAssertion(() => Assert.That(Affected(near), Is.False,
                    "Стазис должен закончиться штатным снятием статуса."));
            }
        });
    }

    [Test]
    public async Task EmpDrainsRealBatteriesOnlyAfterDelayAndWithinRange()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            var battery = server.System<SharedBatterySystem>();
            EntityUid near = default;
            EntityUid far = default;
            await server.WaitPost(() =>
            {
                near = server.EntMan.SpawnEntity("PowerCellHigh", lab.Coordinates);
                far = server.EntMan.SpawnEntity("PowerCellHigh", Position(lab, 10, 10));
                foreach (var uid in new[] { near, far })
                {
                    var component = server.EntMan.GetComponent<BatteryComponent>(uid);
                    battery.SetMaxCharge((uid, component), 5000);
                    battery.SetCharge((uid, component), 5000);
                }
                Activate(lab, "Yellow");
            });
            await lab.Pair.RunSeconds(2);
            await server.WaitAssertion(() => Assert.That(battery.GetCharge(near).Charge, Is.EqualTo(5000)));
            await lab.Pair.RunSeconds(1.3f);
            await server.WaitAssertion(() =>
            {
                Assert.That(battery.GetCharge(near).Charge, Is.EqualTo(3500).Within(1));
                Assert.That(battery.GetCharge(far).Charge, Is.EqualTo(5000));
            });
        });
    }

    [Test]
    public async Task OilExplosionDealsRealDamageAfterWarning()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            EntityUid victim = default;
            await server.WaitPost(() =>
            {
                server.System<SharedTransformSystem>().SetCoordinates(lab.Slime, Position(lab, 0.5f, 0.5f));
                victim = server.EntMan.SpawnEntity("MobCarp", lab.Coordinates);
                server.System<HTNSystem>().SetHTNEnabled(
                    (victim, server.EntMan.GetComponent<HTNComponent>(victim)), false);
                Activate(lab, "Oil");
            });
            await lab.Pair.RunSeconds(4);
            await server.WaitAssertion(() => Assert.That(
                server.System<DamageableSystem>().GetAllDamage((victim, null)).DamageDict.GetValueOrDefault("Blunt"), Is.EqualTo(FixedPoint2.Zero)));
            await lab.Pair.RunSeconds(2);
            await server.WaitAssertion(() => Assert.That(
                server.System<DamageableSystem>().GetAllDamage((victim, null)).DamageDict.GetValueOrDefault("Blunt"), Is.GreaterThan(FixedPoint2.Zero)));
        });
    }

    [Test]
    public async Task ExcessCatalystCannotRepeatPulseAndDeletedSourceCancelsIt()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            EntityUid extract = default;
            await server.WaitPost(() =>
            {
                extract = server.EntMan.SpawnEntity("FishXenoExtractDarkBlue", lab.Coordinates);
                AddCatalyst(lab, extract, "Plasma", 9);
            });
            await server.WaitAssertion(() => Assert.That(
                server.EntMan.GetComponent<FishXenobiologyExtractComponent>(extract).Used, Is.False));
            await server.WaitPost(() => AddCatalyst(lab, extract, "Plasma", 6));
            await lab.Pair.RunSeconds(3.3f);
            await server.WaitPost(() =>
            {
                lab.SetTemperature(293.15f);
                AddCatalyst(lab, extract, "Plasma", 1);
                var deleted = Activate(lab, "DarkBlue");
                server.EntMan.DeleteEntity(deleted);
            });
            await lab.Pair.RunSeconds(3.3f);
            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.HasComponent<FishXenobiologyReactionComponent>(extract), Is.False);
                Assert.That(server.EntMan.GetComponent<TemperatureComponent>(lab.Slime).Temperature, Is.GreaterThan(270));
                var solutions = server.System<SharedSolutionContainerSystem>();
                Assert.That(solutions.TryGetSolution(extract, "extract", out _, out var mixture), Is.True);
                Assert.That(mixture!.GetTotalPrototypeQuantity("Plasma"), Is.EqualTo(FixedPoint2.New(6)));
            });
        });
    }

    [Test]
    public async Task CarriedExtractUsesCurrentPosition()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            EntityUid extract = default;
            await server.WaitAssertion(() =>
            {
                lab.SetTemperature(293.15f);
                extract = Activate(lab, "DarkBlue");
                Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(lab.User, extract), Is.True);
                server.System<SharedTransformSystem>().SetCoordinates(lab.User, new EntityCoordinates(lab.Map, 12, 12));
            });
            await lab.Pair.RunSeconds(3.3f);
            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.GetComponent<TemperatureComponent>(lab.Slime).Temperature, Is.GreaterThan(270));
                Assert.That(server.EntMan.GetComponent<TemperatureComponent>(lab.User).Temperature, Is.LessThan(260));
            });
        });
    }

    private static EntityCoordinates Position(FishXenobiologyTestLab lab, float x, float y)
    {
        return new EntityCoordinates(lab.Coordinates.EntityId, x, y);
    }

    [Test]
    public async Task SummonLimitIncludesPendingAndFreesDeletedOrDeadEntries()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            var entMan = server.EntMan;
            var pending = new List<EntityUid>();
            EntityUid rejected = default;
            await server.WaitPost(() =>
            {
                for (var i = 0; i < 12; i++)
                    pending.Add(Activate(lab, "Gold", "Water", 5));
                rejected = Activate(lab, "Gold", "Water", 5);
            });
            await server.WaitAssertion(() =>
            {
                Assert.That(Summons(lab), Is.Empty);
                Assert.That(entMan.GetComponent<FishXenobiologyExtractComponent>(rejected).Used, Is.False);
                Assert.That(entMan.HasComponent<FishXenobiologyReactionComponent>(rejected), Is.False);
                server.System<SharedSolutionContainerSystem>().TryGetSolution(rejected, "extract", out _, out var mixture);
                Assert.That(mixture!.GetTotalPrototypeQuantity("Water"), Is.EqualTo(FixedPoint2.New(5)));
            });
            await server.WaitAssertion(() =>
            {
                entMan.DeleteEntity(pending[0]);
                Assert.That(server.System<FishXenobiologyExtractSystem>().TryReact(
                    (rejected, entMan.GetComponent<FishXenobiologyExtractComponent>(rejected))), Is.True);
            });
            await lab.Pair.RunSeconds(5.2f);
            await server.WaitAssertion(() =>
            {
                var summons = Summons(lab);
                Assert.That(summons, Has.Count.EqualTo(12));
                var blocked = Activate(lab, "Gold", "Water", 5);
                Assert.That(entMan.GetComponent<FishXenobiologyExtractComponent>(blocked).Used, Is.False);
                server.System<MobStateSystem>().ChangeMobState(summons[0], MobState.Critical);
                Assert.That(server.System<FishXenobiologyExtractSystem>().TryReact(
                    (blocked, entMan.GetComponent<FishXenobiologyExtractComponent>(blocked))), Is.False,
                    "Существо в критическом состоянии всё ещё занимает место в лимите.");
                server.System<MobStateSystem>().ChangeMobState(summons[0], MobState.Dead);
                Assert.That(server.System<FishXenobiologyExtractSystem>().TryReact(
                    (blocked, entMan.GetComponent<FishXenobiologyExtractComponent>(blocked))), Is.True);
                entMan.DeleteEntity(summons[1]);
                var replacement = Activate(lab, "Gold", "Water", 5);
                Assert.That(entMan.GetComponent<FishXenobiologyExtractComponent>(replacement).Used, Is.True);
            });
        });
    }

    [TestCase("Plasma", 10, 2)]
    [TestCase("Blood", 5, 1)]
    [TestCase("Water", 5, 1)]
    public async Task GoldRecipesUseTheirOwnCreaturePools(string reagent, int amount, int count)
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            await server.WaitPost(() => Activate(lab, "Gold", reagent, amount));
            await lab.Pair.RunSeconds(4);
            await server.WaitAssertion(() => Assert.That(Summons(lab), Is.Empty));
            await lab.Pair.RunSeconds(1.2f);
            await server.WaitAssertion(() =>
            {
                var summons = Summons(lab);
                Assert.That(summons, Has.Count.EqualTo(count));
                var allowed = reagent switch
                {
                    "Plasma" => new[] { "MobCarp", "MobGiantSpiderAngry" },
                    "Blood" => new[] { "MobMonkey", "MobCrab" },
                    _ => new[] { "MobMouse", "MobLizard" },
                };
                foreach (var uid in summons)
                    Assert.That(server.EntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID, Is.AnyOf(allowed));
            });
        });
    }

    [Test]
    public async Task SummonsRequireFloorAtActivationAndCompletion()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            EntityUid invalid = default;
            await server.WaitPost(() =>
            {
                invalid = server.EntMan.SpawnEntity("FishXenoExtractGold", Position(lab, 20, 20));
                AddCatalyst(lab, invalid, "Water", 5);
                var moved = Activate(lab, "Gold", "Water", 5);
                server.System<SharedTransformSystem>().SetCoordinates(moved, Position(lab, 20, 20));
            });
            await lab.Pair.RunSeconds(5.2f);
            await server.WaitAssertion(() =>
            {
                Assert.That(Summons(lab), Is.Empty);
                Assert.That(server.EntMan.GetComponent<FishXenobiologyExtractComponent>(invalid).Used, Is.False);
            });
        });
    }

    [Test]
    public async Task SummonBudgetIsIndependentForEachMap()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            var other = await lab.Pair.CreateTestMap();
            try
            {
                await server.WaitAssertion(() =>
                {
                    for (var i = 0; i < 6; i++)
                        Activate(lab, "Gold");
                    var rejected = Activate(lab, "Gold");
                    Assert.That(server.EntMan.GetComponent<FishXenobiologyExtractComponent>(rejected).Used, Is.False);
                    var separate = server.EntMan.SpawnEntity("FishXenoExtractGold", other.GridCoords);
                    AddCatalyst(lab, separate, "Plasma", 10);
                    Assert.That(server.EntMan.GetComponent<FishXenobiologyExtractComponent>(separate).Used, Is.True);
                });
            }
            finally
            {
                await server.WaitPost(() => server.EntMan.DeleteEntity(other.MapUid));
            }
        });
    }

    [Test]
    public async Task NativePourAttributesDelayedReactionToUser()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            await server.WaitAssertion(() =>
            {
                var source = server.EntMan.SpawnEntity("FishXenoExtractMetal", lab.Coordinates);
                AddCatalyst(lab, source, "Plasma", 10);
                var target = server.EntMan.SpawnEntity("FishXenoExtractOrange", lab.Coordinates);
                var solutions = server.System<SharedSolutionContainerSystem>();
                Assert.That(solutions.TryGetSolution(source, "extract", out var from, out _), Is.True);
                Assert.That(solutions.TryGetSolution(target, "extract", out var to, out _), Is.True);
                var amount = server.System<SolutionTransferSystem>().Transfer(
                    new SolutionTransferData(lab.User, source, from!.Value, target, to!.Value, FixedPoint2.New(10)));
                Assert.That(amount, Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(server.EntMan.GetComponent<FishXenobiologyReactionComponent>(target).User, Is.EqualTo(lab.User));
            });
        });
    }

    [Test]
    public async Task LaterPourInSameTickDoesNotClaimSystemReaction()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            await server.WaitAssertion(() =>
            {
                var source = server.EntMan.SpawnEntity("FishXenoExtractMetal", lab.Coordinates);
                AddCatalyst(lab, source, "Nutriment", 1);
                var target = Activate(lab, "Orange");
                var solutions = server.System<SharedSolutionContainerSystem>();
                Assert.That(solutions.TryGetSolution(source, "extract", out var from, out _), Is.True);
                Assert.That(solutions.TryGetSolution(target, "extract", out var to, out _), Is.True);
                server.System<SolutionTransferSystem>().Transfer(
                    new SolutionTransferData(lab.User, source, from!.Value, target, to!.Value, FixedPoint2.New(1)));
                Assert.That(server.EntMan.GetComponent<FishXenobiologyReactionComponent>(target).User, Is.Null,
                    "Поздняя заливка не должна присваивать игроку уже запущенную системную реакцию.");
            });
        });
    }

    private static List<EntityUid> Summons(FishXenobiologyTestLab lab)
    {
        var result = new List<EntityUid>();
        var query = lab.Pair.Server.EntMan.EntityQueryEnumerator<FishXenobiologySummonedComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.MapUid == lab.Map)
                result.Add(uid);
        }
        return result;
    }

    [Test]
    public async Task NativeInjectionAttributesDelayedReactionToUser()
    {
        await FishXenobiologyTestLab.WithLab(async lab =>
        {
            var server = lab.Pair.Server;
            EntityUid target = default;
            await server.WaitAssertion(() =>
            {
                target = server.EntMan.SpawnEntity("FishXenoExtractOrange", lab.Coordinates);
                AddCatalyst(lab, target, "Plasma", 5);
                var syringe = server.EntMan.SpawnEntity("Syringe", lab.Coordinates);
                var solutions = server.System<SharedSolutionContainerSystem>();
                Assert.That(solutions.TryGetSolution(syringe, "injector", out var solution, out _), Is.True);
                Assert.That(solutions.TryAddReagent(solution!.Value, "Plasma", FixedPoint2.New(5)), Is.True);
                Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(lab.User, syringe), Is.True);
                server.System<InjectorSystem>().ToggleMode(
                    (syringe, server.EntMan.GetComponent<InjectorComponent>(syringe)), lab.User,
                    server.ProtoMan.Index((ProtoId<InjectorModePrototype>) "SyringeInjectMode"));
                var interact = new AfterInteractEvent(lab.User, syringe, target, lab.Coordinates, true);
                server.EntMan.EventBus.RaiseLocalEvent(syringe, interact);
                Assert.That(interact.Handled, Is.True);
            });
            await lab.Pair.RunSeconds(1);
            await server.WaitAssertion(() => Assert.That(
                server.EntMan.GetComponent<FishXenobiologyReactionComponent>(target).User, Is.EqualTo(lab.User)));
        });
    }

    private static EntityUid Activate(FishXenobiologyTestLab lab, string breed, string reagent = "Plasma", int amount = 10)
    {
        var extract = lab.Pair.Server.EntMan.SpawnEntity($"FishXenoExtract{breed}", lab.Coordinates);
        AddCatalyst(lab, extract, reagent, amount);
        return extract;
    }

    private static void AddCatalyst(FishXenobiologyTestLab lab, EntityUid extract, string reagent, int amount)
    {
        var solutions = lab.Pair.Server.System<SharedSolutionContainerSystem>();
        if (!solutions.TryGetSolution(extract, "extract", out var solution, out _) ||
            !solutions.TryAddReagent(solution.Value, reagent, FixedPoint2.New(amount)))
            throw new InvalidOperationException("Не удалось добавить катализатор в тестовый экстракт.");
    }
}
