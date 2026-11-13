using System;
using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Fish.Neuro;
using Content.Shared._Fish.Psychosis;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Damage;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.MentalHealth;

[TestFixture]
public sealed class MentalHealthReagentTest : InteractionTest
{
    private static readonly ProtoId<ReagentPrototype> Antipsychotic = "FishAntipsychotic";
    private static readonly ProtoId<ReagentPrototype> Deliriumine = "FishDeliriumine";
    private static readonly ProtoId<ReagentPrototype> Haloperidol = "Haloperidol";
    private static readonly ProtoId<ReagentPrototype> Mannitol = "Mannitol";
    private static readonly ProtoId<DamageTypePrototype> Cellular = "Cellular";
    private static readonly ProtoId<DamageTypePrototype> Heat = "Heat";

    [Test]
    public async Task BrainReagentsHaveGuidebookEffects()
    {
        await Server.WaitAssertion(() =>
        {
            AssertGuidebook(Antipsychotic, effects => effects.OfType<ChemReducePsychosis>().Any());
            AssertGuidebook(Deliriumine, effects => effects.OfType<ChemPsychosis>().Any());
            AssertGuidebook(Haloperidol, effects => effects.OfType<ChemReducePsychosis>().Any());
            AssertGuidebook(Mannitol, effects => effects.OfType<ChemRestoreNeuro>().Any());
        });
    }

    [Test]
    public void AntipsychoticHasThermalSideEffect()
    {
        var reagent = ProtoMan.Index(Antipsychotic);
        var effects = GetEffects(reagent);

        var healthChange = effects.OfType<HealthChange>().SingleOrDefault();
        Assert.That(healthChange, Is.Not.Null, "Нейролит должен иметь побочный эффект HealthChange.");

        var heat = healthChange!.Damage.DamageDict[Heat].Float();
        Assert.That(heat, Is.GreaterThan(0f), "Побочный эффект должен быть термическим уроном.");
        Assert.That(heat, Is.LessThanOrEqualTo(2f), "Термический урон не должен превышать 2 единицы за единицу.");

        var reduce = effects.OfType<ChemReducePsychosis>().Single();
        Assert.That(reduce.ReducePerUnit, Is.GreaterThan(0f));
    }

    [Test]
    public async Task CellularDamageHurtsNeuronsAndAddsPsychosis()
    {
        EntityUid mob = default;

        await Server.WaitPost(() =>
        {
            mob = SEntMan.SpawnEntity("MobHuman", SEntMan.GetCoordinates(PlayerCoords));
            SEntMan.EnsureComponent<PsychosisComponent>(mob);
            var neuro = SEntMan.EnsureComponent<NeuroComponent>(mob);
            neuro.Current = 100f;
        });

        await Server.WaitPost(() =>
        {
            var damageable = SEntMan.System<DamageableSystem>();
            var spec = new DamageSpecifier(ProtoMan.Index(Cellular), FixedPoint2.New(10));
            Assert.That(damageable.TryChangeDamage(mob, spec, ignoreResistances: true, interruptsDoAfters: false));
        });

        await Server.WaitAssertion(() =>
        {
            var neuro = SEntMan.GetComponent<NeuroComponent>(mob);
            Assert.That(neuro.Current, Is.LessThan(100f), "Клеточный урон должен снижать нейроны.");

            var psychosis = SEntMan.GetComponent<PsychosisComponent>(mob);
            Assert.That(psychosis.Sources.ContainsKey("Cellular"), Is.True,
                "Клеточный урон должен добавлять источник психоза.");
        });
    }

    private EntityEffect[] GetEffects(ReagentPrototype reagent)
    {
        return reagent.Metabolisms!.Metabolisms!
            .SelectMany(entry => entry.Value.Effects)
            .ToArray()!;
    }

    private void AssertGuidebook(ProtoId<ReagentPrototype> id, Func<EntityEffect[], bool> predicate)
    {
        var reagent = ProtoMan.Index(id);
        var effects = GetEffects(reagent);

        var target = Array.FindIndex(effects, effect => predicate(new[] { effect }));
        Assert.That(target, Is.GreaterThanOrEqualTo(0), $"У реагента {id} нет нужного эффекта.");

        var text = effects[target].EntityEffectGuidebookText(ProtoMan, Server.EntMan.EntitySysManager);
        Assert.That(text, Is.Not.Null.And.Not.Empty, $"У эффекта реагента {id} нет описания для справочника.");
    }
}
