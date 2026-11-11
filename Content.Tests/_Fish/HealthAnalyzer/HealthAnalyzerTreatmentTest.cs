using System.Collections.Generic;
using System.Linq;
using Content.Client._Fish.HealthAnalyzer.UI;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.Tests._Fish.HealthAnalyzer;

[TestFixture]
public sealed class HealthAnalyzerTreatmentTest
{
    [TestCase("Brute", "Blunt", "Slash", "Bruizine", "Lacerinol")]
    [TestCase("Brute", "Slash", "Piercing", "Lacerinol", "Puncturase")]
    [TestCase("Burn", "Heat", "Shock", "Pyrazine", "Insuzine")]
    [TestCase("Burn", "Cold", "Caustic", "Leporazine", "Sigynate")]
    [TestCase("Toxin", "Poison", "Radiation", "Diphenhydramine", "Arithrazine")]
    public void SevereMixedDamageKeepsBothTreatments(
        string group, string firstType, string secondType, string firstReagent, string secondReagent)
    {
        var damage = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
        {
            [firstType] = 20,
            [secondType] = 35,
        };

        var treatments = FishHealthAnalyzerControl.GetDamageTreatments(
            group, 55, new ProtoId<DamageTypePrototype>[] { firstType, secondType }, damage,
            new HashSet<ProtoId<DamageTypePrototype>>()).ToArray();

        Assert.That(treatments.Select(treatment => treatment.Reagent.Id),
            Is.EqualTo(new[] { firstReagent, secondReagent }));
    }

    [TestCase(29, "Bicaridine", "Bicaridine")]
    [TestCase(30, "Bruizine", "Lacerinol")]
    public void GroupSeverityThresholdIsPreserved(int total, string firstReagent, string secondReagent)
    {
        var damage = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
        {
            ["Blunt"] = 10,
            ["Slash"] = total - 10,
        };

        var treatments = FishHealthAnalyzerControl.GetDamageTreatments(
            "Brute", total, new ProtoId<DamageTypePrototype>[] { "Blunt", "Slash" }, damage,
            new HashSet<ProtoId<DamageTypePrototype>>()).ToArray();

        Assert.That(treatments.Select(treatment => treatment.Reagent.Id),
            Is.EqualTo(new[] { firstReagent, secondReagent }));
    }

    [TestCase(10, "Hyronalin")]
    [TestCase(30, "Arithrazine")]
    public void RadiationUsesActualRadiationMedicine(int total, string expectedReagent)
    {
        var damage = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
        {
            ["Radiation"] = total,
        };

        var treatments = FishHealthAnalyzerControl.GetDamageTreatments(
            "Toxin", total, new ProtoId<DamageTypePrototype>[] { "Poison", "Radiation" }, damage,
            new HashSet<ProtoId<DamageTypePrototype>>()).ToArray();

        Assert.That(treatments.Select(treatment => treatment.Reagent.Id),
            Is.EqualTo(new[] { expectedReagent }));
    }

    [Test]
    public void MinorPoisonAndRadiationUseDifferentMedicines()
    {
        var damage = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
        {
            ["Poison"] = 10,
            ["Radiation"] = 10,
        };

        var treatments = FishHealthAnalyzerControl.GetDamageTreatments(
            "Toxin", 20, new ProtoId<DamageTypePrototype>[] { "Poison", "Radiation" }, damage,
            new HashSet<ProtoId<DamageTypePrototype>>()).ToArray();

        Assert.That(treatments.Select(treatment => treatment.Reagent.Id),
            Is.EqualTo(new[] { "Dylovene", "Hyronalin" }));
    }

    [Test]
    public void UndamagedAndBandagedTypesAreSkipped()
    {
        var damage = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
        {
            ["Blunt"] = 4,
            ["Slash"] = 6,
            ["Piercing"] = 0,
        };

        var treatments = FishHealthAnalyzerControl.GetDamageTreatments(
            "Brute", 10, new ProtoId<DamageTypePrototype>[] { "Blunt", "Slash", "Piercing", "Heat" }, damage,
            new HashSet<ProtoId<DamageTypePrototype>> { "Blunt", "Slash" }).ToArray();

        Assert.That(treatments, Is.Empty);
    }

    [Test]
    public void BasicTreatmentRemainsForTypesWithoutAnAdvancedOption()
    {
        var damage = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
        {
            ["Cellular"] = 30,
            ["Mangleness"] = 5,
        };

        var treatments = FishHealthAnalyzerControl.GetDamageTreatments(
            "Genetic", 35, new ProtoId<DamageTypePrototype>[] { "Cellular", "Mangleness" }, damage,
            new HashSet<ProtoId<DamageTypePrototype>>()).ToArray();

        Assert.That(treatments.Select(treatment => treatment.DamageType.Id),
            Is.EqualTo(new[] { "Cellular", "Mangleness" }));
        Assert.That(treatments.Select(treatment => treatment.Reagent.Id),
            Is.All.EqualTo("Doxarubixadone"));
    }

    [Test]
    public void SharedMedicineKeepsAllReasonsInOneRow()
    {
        var recommendations = new List<FishHealthAnalyzerControl.TreatmentRecommendation>();
        var addedReagents = new HashSet<ProtoId<ReagentPrototype>>();

        FishHealthAnalyzerControl.AddRecommendation(recommendations, addedReagents, "DexalinPlus", "Asphyxiation: 30");
        FishHealthAnalyzerControl.AddRecommendation(recommendations, addedReagents, "DexalinPlus", "Bloodloss: 10");

        Assert.That(recommendations, Has.Count.EqualTo(1));
        Assert.That(recommendations[0].Condition, Is.EqualTo("Asphyxiation: 30\nBloodloss: 10"));
        Assert.That(addedReagents, Has.Count.EqualTo(1));
    }

    [Test]
    public void MultipleRecommendedBruteMedicinesWarnAboutRazorium()
    {
        var recommendations = new HashSet<ProtoId<ReagentPrototype>> { "Bruizine", "Lacerinol" };

        Assert.That(FishHealthAnalyzerControl.HasRazoriumInteraction(
            recommendations,
            new Dictionary<ProtoId<ReagentPrototype>, FixedPoint2>()), Is.True);
    }

    [Test]
    public void ActiveConflictingMedicineWarnsAboutRazorium()
    {
        var recommendations = new HashSet<ProtoId<ReagentPrototype>> { "Puncturase" };
        var activeReagents = new Dictionary<ProtoId<ReagentPrototype>, FixedPoint2>
        {
            ["Bicaridine"] = 5,
        };

        Assert.That(FishHealthAnalyzerControl.HasRazoriumInteraction(recommendations, activeReagents), Is.True);
    }

    [Test]
    public void SameOrUnrelatedMedicineDoesNotWarnAboutRazorium()
    {
        var recommendations = new HashSet<ProtoId<ReagentPrototype>> { "Bicaridine", "Kelotane" };
        var activeReagents = new Dictionary<ProtoId<ReagentPrototype>, FixedPoint2>
        {
            ["Bicaridine"] = 5,
            ["Lacerinol"] = 0,
        };

        Assert.That(FishHealthAnalyzerControl.HasRazoriumInteraction(recommendations, activeReagents), Is.False);
    }
}
