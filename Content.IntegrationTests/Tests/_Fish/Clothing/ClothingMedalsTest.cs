#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Shared._Fish.Clothing;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish.Clothing;

[TestFixture]
public sealed class ClothingMedalsTest : GameTest
{
    private const string Uniform = "ClothingUniformJumpsuitColorGrey";
    private const string FirstMedal = "ClothingNeckGoldmedal";
    private const string SecondMedal = "ClothingNeckBronzeheart";

    /// <summary>
    /// Осмотр персонажа показывает медали, прикреплённые к его форме, и перестаёт показывать их после снятия.
    /// </summary>
    [Test]
    public async Task ExaminingWearerListsAttachedMedals()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var containers = entMan.System<SharedContainerSystem>();
            var inventory = entMan.System<InventorySystem>();
            var examine = entMan.System<ExamineSystemShared>();

            var wearer = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var observer = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var uniform = entMan.SpawnEntity(Uniform, testMap.GridCoords);
            var first = entMan.SpawnEntity(FirstMedal, testMap.GridCoords);
            var second = entMan.SpawnEntity(SecondMedal, testMap.GridCoords);

            Assert.That(inventory.TryEquip(wearer, uniform, "jumpsuit", force: true));

            // Без медалей строки про них нет.
            Assert.That(examine.GetExamineText(wearer, observer).ToMarkup(), Does.Not.Contain(entMan.GetComponent<MetaDataComponent>(first).EntityName));

            Assert.That(containers.TryGetContainer(uniform, "medal1", out var slot1));
            Assert.That(containers.TryGetContainer(uniform, "medal2", out var slot2));
            Assert.That(containers.Insert(first, slot1!));
            Assert.That(containers.Insert(second, slot2!));

            var text = examine.GetExamineText(wearer, observer).ToMarkup();
            Assert.That(text, Does.Contain(entMan.GetComponent<MetaDataComponent>(first).EntityName));
            Assert.That(text, Does.Contain(entMan.GetComponent<MetaDataComponent>(second).EntityName));

            // Последней считается последняя прикреплённая медаль, даже если у неё меньший номер слота.
            Assert.That(containers.Remove(first, slot1!));
            Assert.That(containers.Insert(first, slot1!));
            var order = entMan.GetComponent<ClothingMedalsComponent>(uniform).Order;
            Assert.That(order, Is.EqualTo(new[] { second, first }));

            Assert.That(containers.Remove(first, slot1!));
            Assert.That(containers.Remove(second, slot2!));
            var textAfterRemoval = examine.GetExamineText(wearer, observer).ToMarkup();
            Assert.That(textAfterRemoval, Does.Not.Contain(entMan.GetComponent<MetaDataComponent>(first).EntityName));
            Assert.That(textAfterRemoval, Does.Not.Contain(entMan.GetComponent<MetaDataComponent>(second).EntityName));
        });
    }
}
