using System.Collections.Generic;
using Content.Server._Fish.Netrunner;
using Content.Shared._Fish.Netrunner;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Implants;
using Content.Shared.Store;
using NUnit.Framework;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Netrunner;

public sealed partial class NetrunnerTest
{
    private static readonly ProtoId<ListingPrototype> KitListing = "FishUplinkNetrunnerKit";

    [Test]
    public async Task ShopPurchasesFromClientAndRejectsDuplicate()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid implant = default;
        NetEntity implantNet = default;
        BoundUserInterface shop = null!;
        var id = new ProtoId<NetrunnerAbilityPrototype>("FishNetrunnerOpenAirlock");
        await Server.WaitPost(() =>
        {
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            implant = SEntMan.System<SharedSubdermalImplantSystem>().AddImplant(user, "FishNetrunnerImplant")!.Value;
            implantNet = SEntMan.GetNetEntity(implant);
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            SEntMan.System<NetrunnerSystem>().TryOpenShop((implant, resources), user);
        });
        await Server.WaitAssertion(() =>
        {
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            Assert.That(resources.UnlockedAbilities, Is.Empty);
            Assert.That(resources.GrantedActions, Is.Empty);
            Assert.That(resources.NeuralLinks, Is.EqualTo(12));
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            Assert.That(Client.System<Robust.Client.GameObjects.UserInterfaceSystem>().TryGetOpenUi(
                CEntMan.GetEntity(implantNet), NetrunnerUiKey.Shop, out shop), Is.True);
        });
        await Client.WaitPost(() => shop.SendMessage(new NetrunnerBuyProgramMessage(id)));
        await Pair.RunSeconds(0.5f);
        await Client.WaitPost(() => shop.SendMessage(new NetrunnerBuyUpgradeMessage(NetrunnerUpgrade.Memory)));
        await Pair.RunSeconds(0.5f);
        await Client.WaitPost(() => shop.SendMessage(new NetrunnerBuyProgramMessage(id)));
        await Pair.RunUntilSynced();
        await Server.WaitAssertion(() =>
        {
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            Assert.That(resources.NeuralLinks, Is.EqualTo(4));
            Assert.That(resources.UnlockedAbilities, Has.Count.EqualTo(1));
            Assert.That(resources.GrantedActions, Has.Count.EqualTo(1));
            Assert.That(resources.MaxRam, Is.EqualTo(14));
            Assert.That(resources.Ram, Is.LessThan(resources.MaxRam), "Added capacity must not refill RAM.");
        });
    }

    [Test]
    public async Task KitListingDeliversJacketToBuyer()
    {
        var map = await Pair.CreateTestMap();
        var listing = SProtoMan.Index(KitListing);
        Assert.Multiple(() =>
        {
            Assert.That(listing.ProductEntity, Is.EqualTo(new EntProtoId("FishNetrunnerKit")));
            Assert.That(listing.ProductEvent, Is.TypeOf<NetrunnerKitPurchasedEvent>());
            Assert.That(listing.RaiseProductEventOnUser, Is.True);
        });
        EntityUid user = default;
        EntityUid? held = null;
        var pickedUp = false;
        await Server.WaitPost(() =>
        {
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(user, new NetrunnerKitPurchasedEvent());
            pickedUp = SEntMan.System<SharedHandsSystem>().TryGetActiveItem((user, null), out held);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(pickedUp, Is.True);
            Assert.That(held, Is.Not.Null);
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(held!.Value).EntityPrototype?.ID,
                Is.EqualTo("FishClothingOuterCyberpunkJacket"));
        });
    }

    [Test]
    public async Task HardwareUpgradesAreCappedAndRequireTheOwner()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid implant = default;
        bool outsiderAccepted = true;
        bool fourthMemoryAccepted = true;
        bool fourthSpeedAccepted = true;
        var purchased = new List<bool>();
        await Server.WaitPost(() =>
        {
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var outsider = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            implant = SEntMan.System<SharedSubdermalImplantSystem>().AddImplant(user, "FishNetrunnerImplant")!.Value;
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            resources.NeuralLinks = 100;
            var system = SEntMan.System<NetrunnerSystem>();
            outsiderAccepted = system.TryBuyUpgrade((implant, resources), outsider, NetrunnerUpgrade.Memory);
            for (var i = 0; i < 3; i++)
            {
                purchased.Add(system.TryBuyUpgrade((implant, resources), user, NetrunnerUpgrade.Memory));
                purchased.Add(system.TryBuyUpgrade((implant, resources), user, NetrunnerUpgrade.ActivationSpeed));
            }
            fourthMemoryAccepted = system.TryBuyUpgrade((implant, resources), user, NetrunnerUpgrade.Memory);
            fourthSpeedAccepted = system.TryBuyUpgrade((implant, resources), user, NetrunnerUpgrade.ActivationSpeed);
        });
        await Server.WaitAssertion(() =>
        {
            var resources = SEntMan.GetComponent<NetrunnerImplantComponent>(implant);
            Assert.That(outsiderAccepted, Is.False);
            Assert.That(purchased, Is.All.True);
            Assert.That(fourthMemoryAccepted, Is.False);
            Assert.That(fourthSpeedAccepted, Is.False);
            Assert.That(resources.NeuralLinks, Is.EqualTo(40));
            Assert.That(resources.MaxRam, Is.EqualTo(18));
            Assert.That(resources.Ram, Is.EqualTo(12));
            var duration = SEntMan.System<NetrunnerSystem>().GetCastTime((implant, resources), SProtoMan.Index(NeuralOverload));
            Assert.That(duration.TotalSeconds, Is.EqualTo(7).Within(0.001));
        });
    }
}
