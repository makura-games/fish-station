#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Shared._Fish.Silicons.Borgs.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish.Silicons.Borgs;

/// <summary>
/// Проверяет, что маркер <see cref="BorgNoFallComponent"/>, добавленный на живого борга
/// при runtime-апгрейде до Mk2, доходит до клиента.
/// Апгрейд (<c>BorgSwitchableTypeSystem.Upgrade</c>) добавляет компоненты через
/// <see cref="IEntityManager.AddComponent(EntityUid, string, bool)"/> на уже существующей
/// сущности, а не приходит из прототипа при спавне (MapInit). Без <c>[NetworkedComponent]</c>
/// клиент не получил бы компонент и обрабатывал бы <c>DownAttemptEvent</c> иначе, чем сервер.
/// </summary>
[TestFixture]
[TestOf(typeof(BorgNoFallComponent))]
public sealed class BorgNoFallComponentReplicationTest : GameTest
{
    /// <summary>
    /// Страховочный лимит синхронных тактов в <see cref="WaitForClientComponent"/>.
    /// Это не фиксированная задержка: ожидание завершается сразу, как только компонент
    /// появится на клиенте, а лимит нужен лишь чтобы тест не завис при регрессии.
    /// </summary>
    private const int MaxSyncTicks = 60;

    [Test]
    public async Task RuntimeAddedComponentReplicatesToClient()
    {
        var pair = Pair;
        var server = pair.Server;
        var client = pair.Client;

        var testMap = await pair.CreateTestMap();

        // Спавним «живого борга» и даём сущности дойти до клиента без компонента.
        var serverEnt = await SpawnAtPosition(null, testMap.GridCoords);
        var netEnt = SEntMan.GetNetEntity(serverEnt);

        // RunUntilSynced — встроенное ожидание PVS-рассылки сущностей (без магических тактов).
        await pair.RunUntilSynced();

        await client.WaitAssertion(() =>
        {
            Assert.That(ClientHasComponent(netEnt), Is.False,
                "До апгрейда у клиента не должно быть BorgNoFallComponent");
        });

        // Аналог ApplyMk2Components: компонент добавляется на уже существующую сущность,
        // а не приходит из прототипа при спавне (MapInit).
        await server.WaitPost(() => SEntMan.AddComponent<BorgNoFallComponent>(serverEnt));

        await WaitForClientComponent(netEnt);
    }

    /// <summary>
    /// Крутит пару в синхронных тактах, пока клиент не получит <see cref="BorgNoFallComponent"/>.
    /// Ждём именно целевого состояния, а не заранее заданное число тактов.
    /// </summary>
    private async Task WaitForClientComponent(NetEntity netEnt)
    {
        for (var i = 0; i < MaxSyncTicks; i++)
        {
            if (ClientHasComponent(netEnt))
                return;

            await Pair.RunTicksSync(1);
        }

        Assert.Fail($"BorgNoFallComponent не реплицировался клиенту за {MaxSyncTicks} синхронных тактов");
    }

    private bool ClientHasComponent(NetEntity netEnt)
    {
        return CEntMan.TryGetEntity(netEnt, out var clientEnt)
            && CEntMan.HasComponent<BorgNoFallComponent>(clientEnt.Value);
    }
}
