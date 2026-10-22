#nullable enable
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Fish.BorgRest;
using Content.Shared.Movement.Components;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

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
";

    /// <summary>
    /// Сдвиг 0,0.4 задан в YAML chassis. Тип без собственного <c>spriteOffset</c> не должен
    /// затирать его нулём: сброс ломает визуал холста _Lust (борг висит ниже).
    /// </summary>
    [Test]
    public async Task TypeWithoutSpriteOffsetKeepsChassisOffset()
    {
        await Client.WaitAssertion(() =>
        {
            var borg = CSpawn("FishTestBorgChassisNoOffset");
            var sprite = CComp<SpriteComponent>(borg);

            Assert.That(sprite.Offset, Is.EqualTo(new Vector2(0f, 0.4f)),
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
