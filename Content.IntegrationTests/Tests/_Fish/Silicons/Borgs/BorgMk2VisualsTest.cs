#nullable enable
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Content.Client.Inventory;
using Content.Client.Weapons.Melee;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Fish.BorgRest;
using Content.Shared.Clothing;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Fish.Silicons.Borgs;

/// <summary>
/// Проверяет клиентские визуальные правила Mk2-боргов:
/// null <see cref="BorgTypePrototype.SpriteOffset"/> не сбрасывает сдвиг корня спрайта,
/// а выход из отдыха на ходу возвращает кадр движения, а не стоячий кадр типа.
/// </summary>
/// <remarks>
/// Сценарии воспроизводятся клиентским спавном готового chassis, поэтому проверяются
/// ровно те пути <c>BorgSwitchableTypeSystem.UpdateEntityAppearance</c> и
/// <c>BorgSystem.UpdateBorgRestAppearance</c>, что работают в игре.
/// </remarks>
[TestFixture]
[TestOf(typeof(BorgVisualLayers))]
public sealed class BorgMk2VisualsTest : GameTest
{
    [TestPrototypes]
    private const string TestPrototypes = @"
- type: borgType
  id: FishTestNoOffset
  parent: mining
  spriteBodyState: miner
  spriteBodyMovementState: miner

- type: borgType
  id: FishTestRestMove
  parent: mining
  spriteBodyState: miner
  spriteBodyMovementState: robot
  spriteRestingState: miner_rest

- type: borgType
  id: FishTestWreckMove
  parent: mining
  spriteBodyState: miner
  spriteBodyMovementState: robot
  spriteWreckState: miner_wreck

- type: entity
  id: FishTestBorgChassisNoOffset
  parent: FishBorgChassisMiningMk2
  components:
  - type: BorgSwitchableType
    selectedBorgType: FishTestNoOffset

- type: entity
  id: FishTestBorgChassisRestMove
  parent: FishBorgChassisMiningMk2
  components:
  - type: BorgSwitchableType
    selectedBorgType: FishTestRestMove

- type: entity
  id: FishTestBorgChassisWreckMove
  parent: FishBorgChassisMiningMk2
  components:
  - type: BorgSwitchableType
    selectedBorgType: FishTestWreckMove
";

    /// <summary>
    /// У всех МК2 шаги звучат в нормальной громкости, не наследуя ослабленный
    /// <c>footstepCollection</c> родительского типа. Медицинский МК2 использует
    /// коллекцию инженерного борга вместо hover-звука.
    /// </summary>
    [TestCase("FishGenericMk2", "FootstepCyborgSpider")]
    [TestCase("FishEngineeringMk2", "FootstepCyborgSpider")]
    [TestCase("FishMiningMk2", "FootstepCyborgSpider")]
    [TestCase("FishJanitorMk2", "FootstepJanitorBorg")]
    [TestCase("FishMedicalMk2", "FootstepCyborgSpider")]
    [TestCase("FishServiceMk2", "FootstepBorg")]
    [TestCase("FishPeaceMk2", "FootstepCyborgSpider")]
    [TestCase("FishClownMk2", "FootstepClown")]
    [TestCase("FishSecurityMk2", "FootstepCyborgSpider")]
    public void Mk2FootstepsUseNormalVolume(string borgType, string expectedCollection)
    {
        var prototypes = Pair.Server.ResolveDependency<IPrototypeManager>();

        Assert.That(prototypes.TryIndex<BorgTypePrototype>(borgType, out var prototype), Is.True,
            $"Прототип {borgType} должен существовать");
        Assert.That(prototype!.FootstepCollection, Is.TypeOf<SoundCollectionSpecifier>(),
            $"{borgType} должен задавать footstepCollection");

        var footsteps = (SoundCollectionSpecifier) prototype.FootstepCollection;
        Assert.Multiple(() =>
        {
            Assert.That(footsteps.Collection, Is.Not.Null);
            Assert.That(footsteps.Collection!.Value.Id, Is.EqualTo(expectedCollection));
            Assert.That(footsteps.Params.Volume, Is.EqualTo(0f),
                $"{borgType} не должен наследовать отрицательную громкость шагов");
        });
    }

    /// <summary>
    /// Сдвиг 0,0.5 задан в YAML chassis. Тип без собственного <c>spriteOffset</c> не должен
    /// затирать его нулём: сброс ломает визуал холста _Fish (борг висит ниже).
    /// </summary>
    [Test]
    public async Task TypeWithoutSpriteOffsetKeepsChassisOffset()
    {
        await Client.WaitAssertion(() =>
        {
            var borg = CSpawn("FishTestBorgChassisNoOffset");
            var sprite = CComp<SpriteComponent>(borg);

            Assert.That(sprite.Offset, Is.EqualTo(new Vector2(0f, 0.5f)),
                "Тип с null SpriteOffset не должен сбрасывать сдвиг корня спрайта");
        });
    }

    /// <summary>
    /// Выход из отдыха на ходу не должен оставлять тело на стоячем кадре: раз
    /// <c>SpriteMovementComponent</c> не перерассылает состояние, кадр движения нужно
    /// выбрать прямо в обработчике, иначе борг «идёт стоя».
    /// </summary>
    [Test]
    public async Task ExitingRestWhileMovingRestoresMovementFrame()
    {
        await Client.WaitAssertion(() =>
        {
            var borg = CSpawn("FishTestBorgChassisRestMove");
            var sprite = CComp<SpriteComponent>(borg);
            var appearanceSys = Client.System<AppearanceSystem>();
            var appearance = CEnsureComp<AppearanceComponent>(borg);

            CEnsureComp<SpriteMovementComponent>(borg).IsMoving = true;

            // Вход в отдых: тело — rest-кадр.
            appearanceSys.SetData(borg, BorgRestVisuals.Resting, true, appearance);
            appearanceSys.OnChangeData(borg, sprite, appearance);
            Assert.That(GetBodyState(borg, sprite), Is.EqualTo("miner_rest"),
                "Во время отдыха тело должно быть на rest-кадре");

            // Выход из отдыха, продолжая движение: тело — кадр движения (robot),
            // а не стоячий кадр типа (miner).
            appearanceSys.SetData(borg, BorgRestVisuals.Resting, false, appearance);
            appearanceSys.OnChangeData(borg, sprite, appearance);
            Assert.That(GetBodyState(borg, sprite), Is.EqualTo("robot"),
                "При выходе из отдыха на ходу тело должно вернуться в кадр движения");
        });
    }

    /// <summary>
    /// Во время отдыха скрываются слои головы и пояса, новые слои экипировки тоже
    /// скрываются, а повторное обновление appearance не возвращает has-mind подсветку.
    /// </summary>
    [Test]
    public async Task RestHidesEquipmentAndKeepsMindLightHidden()
    {
        await Client.WaitAssertion(() =>
        {
            var borg = CSpawn("FishTestBorgChassisRestMove");
            var sprite = CComp<SpriteComponent>(borg);
            var appearanceSys = Client.System<AppearanceSystem>();
            var spriteSys = Client.System<SpriteSystem>();
            var appearance = CEnsureComp<AppearanceComponent>(borg);
            var slots = CEnsureComp<InventorySlotsComponent>(borg);

            AddTestLayer(borg, sprite, spriteSys, "test-head");
            AddTestLayer(borg, sprite, spriteSys, "test-belt");
            slots.VisualLayerKeys["head"] = new HashSet<string> { "test-head" };
            slots.VisualLayerKeys["belt"] = new HashSet<string> { "test-belt" };

            appearanceSys.SetData(borg, BorgVisuals.HasPlayer, true, appearance);
            appearanceSys.SetData(borg, BorgRestVisuals.Resting, true, appearance);
            appearanceSys.OnChangeData(borg, sprite, appearance);

            Assert.Multiple(() =>
            {
                Assert.That(IsLayerVisible(borg, sprite, "test-head"), Is.False,
                    "Слой головы должен быть скрыт во время отдыха");
                Assert.That(IsLayerVisible(borg, sprite, "test-belt"), Is.False,
                    "Слой пояса должен быть скрыт во время отдыха");
                Assert.That(IsLayerVisible(borg, sprite, BorgVisualLayers.Light), Is.False,
                    "Подсветка has-mind не должна показываться во время отдыха");
            });

            // Повторное обновление appearance имитирует контейнерное событие при смене
            // экипировки: ванильный UpdateBorgAppearance не должен вернуть Light.
            appearanceSys.OnChangeData(borg, sprite, appearance);
            Assert.That(IsLayerVisible(borg, sprite, BorgVisualLayers.Light), Is.False,
                "Обновление экипировки не должно возвращать подсветку в отдыхе");

            AddTestLayer(borg, sprite, spriteSys, "test-head-new");
            slots.VisualLayerKeys["head"].Add("test-head-new");
            CEntMan.EventBus.RaiseLocalEvent(
                borg,
                new EquipmentVisualsUpdatedEvent(borg, "head", new HashSet<string> { "test-head-new" }),
                true);

            Assert.That(IsLayerVisible(borg, sprite, "test-head-new"), Is.False,
                "Новый слой головы, надетый во время отдыха, должен быть скрыт");

            appearanceSys.SetData(borg, BorgRestVisuals.Resting, false, appearance);
            appearanceSys.OnChangeData(borg, sprite, appearance);

            Assert.Multiple(() =>
            {
                Assert.That(IsLayerVisible(borg, sprite, "test-head"), Is.True,
                    "После отдыха слой головы должен снова отображаться");
                Assert.That(IsLayerVisible(borg, sprite, "test-belt"), Is.True,
                    "После отдыха слой пояса должен снова отображаться");
                Assert.That(IsLayerVisible(borg, sprite, "test-head-new"), Is.True,
                    "Новый слой головы должен отображаться после отдыха");
                Assert.That(IsLayerVisible(borg, sprite, BorgVisualLayers.Light), Is.True,
                    "После отдыха подсветка has-mind должна вернуться");
            });
        });
    }

    /// <summary>
    /// После wreck-кадра возврат в Alive должен выбрать кадр движения, если борг движется,
    /// а не стоячий кадр типа.
    /// </summary>
    [Test]
    public async Task MovingAliveBorgUsesMovementFrameAfterWreck()
    {
        await Client.WaitAssertion(() =>
        {
            var borg = CSpawn("FishTestBorgChassisWreckMove");
            var sprite = CComp<SpriteComponent>(borg);
            var appearanceSys = Client.System<AppearanceSystem>();
            var appearance = CEnsureComp<AppearanceComponent>(borg);

            CEnsureComp<SpriteMovementComponent>(borg).IsMoving = true;

            appearanceSys.SetData(borg, MobStateVisuals.State, MobState.Dead, appearance);
            appearanceSys.OnChangeData(borg, sprite, appearance);
            Assert.That(GetBodyState(borg, sprite), Is.EqualTo("miner_wreck"),
                "В мёртвом состоянии тело должно быть на wreck-кадре");

            appearanceSys.SetData(borg, MobStateVisuals.State, MobState.Alive, appearance);
            appearanceSys.OnChangeData(borg, sprite, appearance);
            Assert.That(GetBodyState(borg, sprite), Is.EqualTo("robot"),
                "При возврате в Alive движущийся борг должен использовать кадр движения");
        });
    }

    /// <summary>
    /// Тип без собственного <c>spriteOffset</c> не должен возвращать <see cref="Vector2.Zero"/>
    /// из lunge-анимации: базой остаётся offset из прототипа chassis.
    /// </summary>
    [Test]
    public async Task LungeOffsetFallsBackToChassisSpriteOffset()
    {
        await Client.WaitAssertion(() =>
        {
            var borg = CSpawn("FishTestBorgChassisNoOffset");
            var melee = Client.System<MeleeWeaponSystem>();
            var method = typeof(MeleeWeaponSystem).GetMethod(
                "GetBaseSpriteOffset",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, "GetBaseSpriteOffset должен существовать");

            var offset = (Vector2) method!.Invoke(melee, new object[] { borg })!;
            Assert.That(offset, Is.EqualTo(new Vector2(0f, 0.5f)),
                "Lunge-анимация должна сохранять offset chassis при null SpriteOffset типа");
        });
    }

    private static void AddTestLayer(EntityUid uid, SpriteComponent sprite, SpriteSystem spriteSys, string key)
    {
        var index = spriteSys.AddLayer(
            (uid, sprite),
            new SpriteSpecifier.Rsi(new ResPath("/Textures/Interface/Misc/progress_bar.rsi"), "icon"));
        spriteSys.LayerMapSet((uid, sprite), key, index);
        spriteSys.LayerSetVisible((uid, sprite), index, true);
    }

    private bool IsLayerVisible(EntityUid uid, SpriteComponent sprite, string key)
    {
        var spriteSys = Client.System<SpriteSystem>();
        return spriteSys.TryGetLayer((uid, sprite), key, out var layer, false) && layer.Visible;
    }

    private bool IsLayerVisible(EntityUid uid, SpriteComponent sprite, Enum key)
    {
        var spriteSys = Client.System<SpriteSystem>();
        return spriteSys.TryGetLayer((uid, sprite), key, out var layer, false) && layer.Visible;
    }

    private T CEnsureComp<T>(EntityUid uid)
        where T : IComponent, new()
    {
        return CEntMan.HasComponent<T>(uid)
            ? CEntMan.GetComponent<T>(uid)
            : CEntMan.AddComponent<T>(uid);
    }

    private string? GetBodyState(EntityUid uid, SpriteComponent sprite)
    {
        var spriteSys = Client.System<SpriteSystem>();
        var layer = spriteSys.LayerMapGet((uid, sprite), BorgVisualLayers.Body);
        return spriteSys.LayerGetRsiState((uid, sprite), layer).Name;
    }
}
