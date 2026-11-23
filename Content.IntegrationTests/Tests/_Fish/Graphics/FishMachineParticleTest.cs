using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Fish.Effects;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Power;
using Content.Shared.Starlight.Energy.Supermatter;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Fish.Graphics;

public sealed class FishMachineParticleTest : InteractionTest
{
    [Test]
    public async Task BorgSmokeRemainsOnFloorAndStopsAfterRepair()
    {
        var borg = await Spawn("BorgChassisGeneric");
        await Server.WaitPost(() => Server.System<DamageableSystem>().TryChangeDamage(ToServer(borg),
            new DamageSpecifier { DamageDict = { ["Blunt"] = 60 } }, ignoreResistances: true, ignoreVariance: true));
        Assert.That(await WaitForParticles("FishSmokeParticle", 1), Is.True);
        await Client.WaitAssertion(() =>
        {
            var query = CEntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out _, out var meta, out var transform))
            {
                if (meta.EntityPrototype?.ID == "FishSmokeParticle")
                    Assert.That(transform.ParentUid, Is.EqualTo(MapData.CGridUid));
            }
        });

        await Server.WaitPost(() => Server.System<DamageableSystem>().SetAllDamage(ToServer(borg), 0));
        await RunTicks(Pair.SecondsToTicks(1.5f));
        Assert.That(await WaitForParticles("FishSmokeParticle", 1), Is.False);
    }

    [Test]
    public async Task FallingIntegrityChipsThenChargePrecedesActualLightning()
    {
        var rodPosition = new NetCoordinates(TargetCoords.NetEntity, TargetCoords.Position + new Vector2(0, 2));
        await SetTile(Plating, new NetCoordinates(TargetCoords.NetEntity, TargetCoords.Position + Vector2.UnitY), MapData.Grid);
        await SetTile(Plating, rodPosition, MapData.Grid);
        var crystal = await Spawn("SupermatterCrystal");
        var rod = await Spawn("TeslaGroundingRod", rodPosition);

        await Server.WaitPost(() =>
        {
            var component = SComp<SupermatterComponent>(ToServer(crystal));
            component.Activated = true;
            component.AccBreak = 42;
        });
        Assert.That(await WaitForParticles("FishSupermatterChipParticle", 4), Is.True);
        await Server.WaitAssertion(() => Assert.That(SComp<SupermatterComponent>(ToServer(crystal)).Durability.Float(), Is.LessThan(100f)));
        await RunTicks(Pair.SecondsToTicks(0.55f)); // Даём осколкам завершить разлёт до следующей зарядки.
        await Server.WaitPost(() => SComp<SupermatterComponent>(ToServer(crystal)).AccLighting = 6);

        Assert.That(await WaitForParticles("FishSupermatterChipParticle", 5), Is.True);
        bool RodWasHit() => Server.System<SharedAppearanceSystem>().TryGetData<bool>(
            ToServer(rod), TeslaCoilVisuals.Lightning, out var struck) && struck;

        await Server.WaitAssertion(() => Assert.That(RodWasHit(), Is.False, "The crystal must draw in its chips before firing."));
        var distances = new Dictionary<EntityUid, float>();
        await Client.WaitAssertion(() =>
        {
            Client.System<FishMachineParticleSystem>().FrameUpdate(0f);
            var center = ToClient(crystal);
            var chips = CEntMan.EntityQueryEnumerator<MetaDataComponent, SpriteComponent, TransformComponent>();
            while (chips.MoveNext(out var uid, out var meta, out var sprite, out var xform))
            {
                if (meta.EntityPrototype?.ID == "FishSupermatterChipParticle" && xform.ParentUid == center)
                    distances.Add(uid, sprite.Offset.LengthSquared());
            }
            Assert.That(distances.Count, Is.GreaterThanOrEqualTo(5));
        });

        await RunTicks(Pair.SecondsToTicks(0.1f));
        await Server.WaitAssertion(() => Assert.That(RodWasHit(), Is.False));
        await Client.WaitAssertion(() =>
        {
            Client.System<FishMachineParticleSystem>().FrameUpdate(0f);
            foreach (var (uid, before) in distances)
                Assert.That(CEntMan.GetComponent<SpriteComponent>(uid).Offset.LengthSquared(), Is.LessThan(before),
                    $"Shard {uid} should approach the crystal before the strike.");
        });

        await RunTicks(Pair.SecondsToTicks(0.4f));
        await Server.WaitAssertion(() => Assert.That(RodWasHit(), Is.True, "The original lightning must strike the grounding rod."));
    }

    private async Task<bool> WaitForParticles(string prototype, int minimum)
    {
        for (var i = 0; i < Pair.SecondsToTicks(1.8f); i += 2)
        {
            await RunTicks(2);
            var found = false;
            await Client.WaitPost(() => found = CEntMan.EntityQuery<MetaDataComponent>()
                .Count(meta => meta.EntityPrototype?.ID == prototype) >= minimum);
            if (found)
                return true;
        }

        return false;
    }
}
