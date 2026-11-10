#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared._Fish.Ipc;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Overlays;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Ipc;

/// <summary>
/// Пункты ревью: ремонт лечит, при нуле заряда модули молчат, в крите нет цикла жужжания.
/// </summary>
[TestFixture]
public sealed class FishIpcReviewFixesTest : FishIpcGameTest
{
    private static readonly string[] CritEmotes = ["Buzz-Two", "Ping", "SevereCough", "DefaultDeathgasp"];
    private static readonly ProtoId<EmoteSoundsPrototype> IpcVoice = "UnisexIpc";

    [Test]
    public async Task NanoRepair_HealsBlunt_NotHeat()
    {
        EntityUid ipc = default;

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            ipc = SSpawn("MobIpc");
            InstallModule(ipc, "FishIpcModuleNanoRepair");
            Hurt(ipc, "Blunt", 20);
            Hurt(ipc, "Heat", 20);
        });

        await RunSeconds(4);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                // 0.15/с * 4 с = 0.6, с округлением тиков около 19.4.
                Assert.That(Damage(ipc, "Blunt"), Is.EqualTo(19.4f).Within(0.25f));
                Assert.That(Damage(ipc, "Heat"), Is.EqualTo(20f));
            });
        });
    }

    [Test]
    public async Task ReactiveRepair_HealsHeatAndShock_NotBlunt()
    {
        EntityUid ipc = default;

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            ipc = SSpawn("MobIpc");
            InstallModule(ipc, "FishIpcModuleReactiveRepair");
            Hurt(ipc, "Blunt", 20);
            Hurt(ipc, "Heat", 20);
            Hurt(ipc, "Shock", 20);
        });

        await RunSeconds(4);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(Damage(ipc, "Heat"), Is.LessThan(18.5f));
                Assert.That(Damage(ipc, "Shock"), Is.LessThan(18.5f));
                Assert.That(Damage(ipc, "Blunt"), Is.EqualTo(20f));
            });
        });
    }

    [Test]
    public async Task RepairAndVision_StopAtZeroCharge_ThenResume()
    {
        EntityUid ipc = default;
        float bluntAtZero = 0;

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            ipc = SSpawn("MobIpc");
            InstallModule(ipc, "FishIpcModuleNanoRepair");
            InstallModule(ipc, "FishIpcModuleMedHud");
            Hurt(ipc, "Blunt", 30);
            Assert.That(SEntMan.HasComponent<ShowHealthBarsComponent>(ipc), Is.True);
            SetCellCharge(ipc, 0f);
        });

        await RunSeconds(1);

        await Server.WaitPost(() =>
        {
            Assert.That(SComp<FishIpcComponent>(ipc).PowerDown, Is.True);
            Assert.That(SEntMan.HasComponent<ShowHealthBarsComponent>(ipc), Is.False);
            bluntAtZero = Damage(ipc, "Blunt");
        });

        await RunSeconds(3);

        await Server.WaitPost(() =>
        {
            Assert.That(Damage(ipc, "Blunt"), Is.EqualTo(bluntAtZero));
            SetCellCharge(ipc, 720f);
        });

        await RunSeconds(1);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<FishIpcComponent>(ipc).PowerDown, Is.False);
            Assert.That(SEntMan.HasComponent<ShowHealthBarsComponent>(ipc), Is.True);
        });

        await RunSeconds(4);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Damage(ipc, "Blunt"), Is.LessThan(bluntAtZero - 0.4f));
        });
    }

    [Test]
    public async Task Crit_BuzzIsOnHit_EmoteIsOneOfFourEveryThirtyToFiftySeconds()
    {
        EntityUid ipc = default;
        var seen = new HashSet<string>();

        await Server.WaitPost(() =>
        {
            DisableDamageVariance();
            ipc = SSpawn("MobIpc");
            Hurt(ipc, "Blunt", 30);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<FishEmitBuzzWhileDamagedComponent>(ipc).BuzzCount, Is.EqualTo(1));
        });

        await Server.WaitPost(() => Hurt(ipc, "Blunt", 10));

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<FishEmitBuzzWhileDamagedComponent>(ipc).BuzzCount, Is.EqualTo(1), "кулдаун не даёт второму удару пищать сразу");
        });

        await Server.WaitPost(() =>
        {
            var buzz = SComp<FishEmitBuzzWhileDamagedComponent>(ipc);
            buzz.NextBuzz = TimeSpan.Zero;
            Hurt(ipc, "Blunt", 80);
        });

        await RunSeconds(8);

        await Server.WaitAssertion(() =>
        {
            var buzz = SComp<FishEmitBuzzWhileDamagedComponent>(ipc);
            Assert.Multiple(() =>
            {
                Assert.That(SComp<MobStateComponent>(ipc).CurrentState, Is.EqualTo(MobState.Critical));
                Assert.That(buzz.BuzzCount, Is.EqualTo(2), "крит сам по себе не крутит жужжание");
                Assert.That(buzz.CritEmoteScheduled, Is.True);
                Assert.That(buzz.LastCritEmote, Is.Null);
                Assert.That(buzz.LastCritEmoteDelay, Is.InRange(30f, 50f));
                Assert.That(buzz.CritEmotes.Select(id => id.Id), Is.EquivalentTo(CritEmotes));
            });
        });

        await Server.WaitPost(() =>
        {
            var buzz = SComp<FishEmitBuzzWhileDamagedComponent>(ipc);
            buzz.CritEmoteMinDelay = 0f;
            buzz.CritEmoteMaxDelay = 0f;
            buzz.NextCritEmote = SGameTiming.CurTime;
        });

        for (var i = 0; i < 40 && seen.Count < CritEmotes.Length; i++)
        {
            await RunTicksSync(1);
            await Server.WaitAssertion(() =>
            {
                var id = SComp<FishEmitBuzzWhileDamagedComponent>(ipc).LastCritEmote;
                if (id != null)
                    seen.Add(id);
            });
        }

        await Server.WaitAssertion(() =>
        {
            Assert.That(seen, Is.EquivalentTo(CritEmotes));
        });
    }

    [Test]
    public async Task CritEmotes_AreAllowedAndHaveSounds()
    {
        EntityUid ipc = default;

        await Server.WaitPost(() => ipc = SSpawn("MobIpc"));

        await Server.WaitAssertion(() =>
        {
            var sounds = SProtoMan.Index(IpcVoice);
            var chat = Server.System<ChatSystem>();

            Assert.Multiple(() =>
            {
                foreach (var id in CritEmotes)
                {
                    Assert.That(SProtoMan.TryIndex<EmotePrototype>(id, out var emote), Is.True, id);
                    Assert.That(chat.AllowedToUseEmote(ipc, emote!), Is.True, id);
                    Assert.That(sounds.Sounds.ContainsKey(id), Is.True, id);
                    Assert.That(sounds.Sounds[id], Is.Not.Null, id);
                }
            });
        });
    }
}
