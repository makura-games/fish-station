#nullable enable
using Content.Server.Station.Systems;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Loadouts;

[TestFixture]
[TestOf(typeof(RoleLoadoutPrototype))]
public sealed class FishCentcommMedicalDoctorPdaTest
{
    [Test]
    public async Task CentcommMedicalDoctor_ReceivesExactlyOneMedicalPda()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ProtoMan;
        var entMan = server.EntMan;

        ProtoId<RoleLoadoutPrototype> doctorRole = "JobCentcommMedicalDoctor";
        ProtoId<LoadoutGroupPrototype> doctorPdaGroup = "MedicalDoctorPDA";
        ProtoId<LoadoutPrototype> doctorPdaLoadout = "MedicalDoctorPDA";
        EntProtoId medicalPda = "MedicalPDA";
        ProtoId<StartingGearPrototype> bartenderGearId = "CentcommBartenderGear";
        EntProtoId bartenderPda = "BartenderPDA";
        ProtoId<StartingGearPrototype> officerGearId = "CentCommOfficerGear";
        EntProtoId centcomPda = "CentcomPDA";
        ProtoId<JobPrototype> doctorJob = "CentcommMedicalDoctor";

        await server.WaitAssertion(() =>
        {
            Assert.That(protoMan.TryIndex(doctorRole, out var roleLoadout), Is.True);
            Assert.That(roleLoadout!.Groups, Does.Contain(doctorPdaGroup));

            Assert.That(protoMan.TryIndex(doctorPdaLoadout, out var pdaLoadout), Is.True);
            Assert.That(pdaLoadout!.Equipment.TryGetValue("id", out var pdaProto), Is.True);
            Assert.That(pdaProto, Is.EqualTo(medicalPda));

            // Другие роли ЦК не должны потерять свой КПК в startingGear.
            Assert.That(protoMan.TryIndex(bartenderGearId, out var bartenderGear), Is.True);
            Assert.That(bartenderGear!.Equipment["id"], Is.EqualTo(bartenderPda));

            Assert.That(protoMan.TryIndex(officerGearId, out var officerGear), Is.True);
            Assert.That(officerGear!.Equipment["id"], Is.EqualTo(centcomPda));
        });

        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var stationSystem = entMan.System<StationSpawningSystem>();
            var inventory = entMan.System<InventorySystem>();
            var profile = new HumanoidCharacterProfile();

            var doctor = stationSystem.SpawnPlayerMob(
                testMap.GridCoords,
                job: doctorJob,
                profile,
                station: null);

            Assert.That(inventory.TryGetSlotEntity(doctor, "id", out var idUid), Is.True, "У врача ЦК должен быть предмет в слоте id");
            Assert.That(entMan.GetComponent<MetaDataComponent>(idUid!.Value).EntityPrototype?.ID, Is.EqualTo(medicalPda.Id));
            Assert.That(entMan.HasComponent<PdaComponent>(idUid.Value), Is.True);

            var pdaCount = 0;
            var slots = inventory.GetSlotEnumerator(doctor);
            while (slots.NextItem(out var item, out _))
            {
                if (entMan.HasComponent<PdaComponent>(item))
                    pdaCount++;
            }

            Assert.That(pdaCount, Is.EqualTo(1), "У врача ЦК должен быть ровно один КПК");

            entMan.DeleteEntity(doctor);
        });

        await pair.CleanReturnAsync();
    }
}
