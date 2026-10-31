using System.Collections.Generic;
using System.Linq;
using Content.Server._Sunrise.SponsorValidation;
using Content.Shared._Sunrise.Pets;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;

namespace Content.IntegrationTests.Tests._Fish.Preferences;

/// <summary>
/// Все roundStart расы доступны без спонсорских прототипов;
/// остальные спонсорские ограничения (питомцы и т.п.) не обходятся.
/// </summary>
[TestFixture]
[TestOf(typeof(HumanoidCharacterProfile))]
public sealed class FishSpeciesAccessFreeTests
{
    [Test]
    public async Task EnsureValid_KeepsEveryRoundStartSpecies_WithoutSponsorPrototypes()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var session = pair.Player;
            Assert.That(session, Is.Not.Null, "Нужна сессия игрока для EnsureValid");

            var roundStartSpecies = server.ProtoMan
                .EnumeratePrototypes<SpeciesPrototype>()
                .Where(species => species.RoundStart)
                .Select(species => species.ID)
                .OrderBy(id => id)
                .ToArray();

            Assert.That(roundStartSpecies, Is.Not.Empty, "Ожидались roundStart расы");

            foreach (var speciesId in roundStartSpecies)
            {
                var profile = HumanoidCharacterProfile.DefaultWithSpecies(speciesId);
                profile.EnsureValid(session!, server.EntMan.EntitySysManager.DependencyCollection, []);

                Assert.That(
                    profile.Species.Id,
                    Is.EqualTo(speciesId),
                    $"EnsureValid сбросил расу {speciesId} при пустых sponsorPrototypes");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RandomSpecies_CanIncludeFormerlySponsorSpecies()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var formerlySponsor = new[] { "Resomi", "Milira", "Predator", "HumanoidXeno" };
            foreach (var speciesId in formerlySponsor)
            {
                Assert.That(
                    server.ProtoMan.TryIndex<SpeciesPrototype>(speciesId, out var proto) && proto.RoundStart,
                    Is.True,
                    $"Раса {speciesId} должна существовать и быть roundStart");
            }

            var seen = new HashSet<string>();
            for (var i = 0; i < 500; i++)
            {
                seen.Add(HumanoidCharacterProfile.RandomSpecies().ID);
                if (formerlySponsor.All(id => seen.Contains(id)))
                    break;
            }

            Assert.That(
                formerlySponsor.Intersect(seen).Any(),
                Is.True,
                "RandomSpecies должен уметь выбирать ранее спонсорские расы");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SponsorPetValidation_StillRequiresSponsorAccess()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var session = pair.Player;
            Assert.That(session, Is.Not.Null);

            var sponsorPet = server.ProtoMan
                .EnumeratePrototypes<PetSelectionPrototype>()
                .FirstOrDefault(pet => pet.SponsorOnly);

            Assert.That(sponsorPet, Is.Not.Null, "Нужен хотя бы один SponsorOnly питомец для регрессии");

            var validation = server.EntMan.System<SponsorValidationSystem>();
            Assert.That(
                validation.ValidatePetSelection(sponsorPet!.ID, session!.UserId),
                Is.False,
                "Спонсорский питомец не должен быть доступен без доната");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AllRoundStartSpecies_HaveSponsorOnlyDisabledOrIgnored()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var locked = server.ProtoMan
                .EnumeratePrototypes<SpeciesPrototype>()
                .Where(species => species.RoundStart && species.SponsorOnly)
                .Select(species => species.ID)
                .ToArray();

            Assert.That(
                locked,
                Is.Empty,
                $"Остались SponsorOnly roundStart расы: {string.Join(", ", locked)}");
        });

        await pair.CleanReturnAsync();
    }
}
