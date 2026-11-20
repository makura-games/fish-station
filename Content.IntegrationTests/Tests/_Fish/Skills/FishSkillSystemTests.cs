#nullable enable
using System.Linq;
using Content.Server._Fish.Skills;
using Content.Shared._Fish.Skills;
using Content.Shared.DoAfter;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;

namespace Content.IntegrationTests.Tests._Fish.Skills;

/// <summary>
/// Навыки выдаются бюджетом роли при спавне (роль = «бэкграунд» раунда).
/// В HumanoidCharacterProfile навыки не хранятся — сериализуется SkillsetComponent сущности.
/// </summary>
[TestFixture]
[TestOf(typeof(SkillSystem))]
public sealed class FishSkillSystemTests
{
    [Test]
    public async Task JobBudget_GrantsMinSkillsAndPoints()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var proto = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            Assert.That(proto.TryIndex<JobSkillBudgetPrototype>("FishStationEngineer", out var engineerBudget), Is.True);
            Assert.That(engineerBudget!.GetJobId(), Is.EqualTo((ProtoId<JobPrototype>)"StationEngineer"));

            var mob = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var skills = entMan.System<SkillSystem>();
            skills.EnsureSkillset(mob, "StationEngineer");

            Assert.That(entMan.TryGetComponent(mob, out SkillsetComponent? skillset), Is.True);
            Assert.That(skillset!.BudgetGranted, Is.True);
            Assert.That(skillset.JobId, Is.EqualTo((ProtoId<JobPrototype>)"StationEngineer"));
            Assert.That(skillset.TotalPoints, Is.EqualTo(engineerBudget.SkillPoints));
            Assert.That(skillset.RemainingPoints, Is.EqualTo(engineerBudget.SkillPoints));
            Assert.That(skillset.Locked, Is.False);
            Assert.That(skillset.RemainingPoints, Is.GreaterThan(0));

            foreach (var (skill, min) in engineerBudget.MinSkills)
            {
                Assert.That(skillset.Skills.GetValueOrDefault(skill), Is.EqualTo(min),
                    $"Минимум роли для {skill}");
            }

            // До Confirm provisional-уровни не должны влиять на эффекты.
            var jobMinConstruction = skills.GetMinSkill("StationEngineer", "FishSkillConstruction");
            skillset.Skills["FishSkillConstruction"] = SkillLevels.Master;
            Assert.That(skills.GetEffectiveSkillValue(mob, "FishSkillConstruction"), Is.EqualTo(jobMinConstruction));

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ZeroPointBudget_AutoLocksWithMins()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var proto = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            Assert.That(proto.TryIndex<JobSkillBudgetPrototype>("FishERTSecurity", out var budget), Is.True);
            Assert.That(budget!.SkillPoints, Is.EqualTo(0));

            var skills = entMan.System<SkillSystem>();
            var mob = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            skills.EnsureSkillset(mob, "ERTSecurity");

            Assert.That(entMan.TryGetComponent(mob, out SkillsetComponent? skillset), Is.True);
            Assert.That(skillset!.Locked, Is.True);
            Assert.That(skillset.RemainingPoints, Is.EqualTo(0));
            Assert.That(skills.GetEffectiveSkillValue(mob, "FishSkillWeapons"),
                Is.EqualTo(budget.MinSkills["FishSkillWeapons"]));

            // Без skillset — Baseline (госты/нюкеры без job).
            var bare = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            Assert.That(skills.GetEffectiveSkillValue(bare, "FishSkillConstruction"), Is.EqualTo(SkillLevels.Baseline));

            entMan.DeleteEntity(mob);
            entMan.DeleteEntity(bare);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ManualStudy_RaisesSkillAfterConfirm()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var timing = server.Timing;

        EntityUid mob = default;
        EntityUid manual = default;

        await server.WaitPost(() =>
        {
            var skills = entMan.System<SkillSystem>();
            mob = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            entMan.EnsureComponent<DoAfterComponent>(mob);
            skills.EnsureSkillset(mob, "StationEngineer");

            var skillset = entMan.GetComponent<SkillsetComponent>(mob);
            skillset.Skills["FishSkillConstruction"] = SkillLevels.Unskilled;
            skillset.Locked = true;
            entMan.Dirty(mob, skillset);

            manual = entMan.SpawnEntity("FishSkillManualConstructionBasic", MapCoordinates.Nullspace);
            var manualComp = entMan.GetComponent<SkillTrainingManualComponent>(manual);
            manualComp.StudyTime = timing.TickPeriod;
            Assert.That(skills.TryStartManualStudy(mob, (manual, manualComp)), Is.True);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var skills = entMan.System<SkillSystem>();
            Assert.That(skills.GetSkillValue(mob, "FishSkillConstruction"), Is.EqualTo(SkillLevels.Basic));
            Assert.That(skills.GetEffectiveSkillValue(mob, "FishSkillConstruction"), Is.EqualTo(SkillLevels.Basic));
            Assert.That(
                entMan.GetComponent<SkillsetComponent>(mob).CompletedTrainings,
                Does.Contain(SkillMath.MakeTrainingKey("FishSkillConstruction", SkillLevels.Basic)));

            var manualComp = entMan.GetComponent<SkillTrainingManualComponent>(manual);
            Assert.That(skills.TryStartManualStudy(mob, (manual, manualComp)), Is.False);

            entMan.DeleteEntity(manual);
            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SkillsetComponent_SerializesRoundTrip()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var seri = server.ResolveDependency<ISerializationManager>();
            var original = new SkillsetComponent
            {
                Skills =
                {
                    ["FishSkillConstruction"] = SkillLevels.Trained,
                    ["FishSkillMedical"] = SkillLevels.Basic,
                },
                RemainingPoints = 4,
                TotalPoints = 16,
                JobId = "StationEngineer",
                BudgetGranted = true,
                Locked = true,
                CompletedTrainings = { "FishSkillConstruction:2" },
            };

            var node = seri.WriteValue(original, alwaysWrite: true);
            Assert.That(node, Is.InstanceOf<MappingDataNode>());

            var copy = seri.Read<SkillsetComponent>(node, notNullableOverride: true);
            Assert.That(copy.RemainingPoints, Is.EqualTo(original.RemainingPoints));
            Assert.That(copy.TotalPoints, Is.EqualTo(original.TotalPoints));
            Assert.That(copy.JobId, Is.EqualTo(original.JobId));
            Assert.That(copy.BudgetGranted, Is.EqualTo(original.BudgetGranted));
            Assert.That(copy.Locked, Is.EqualTo(original.Locked));
            Assert.That(copy.Skills["FishSkillConstruction"], Is.EqualTo(SkillLevels.Trained));
            Assert.That(copy.Skills["FishSkillMedical"], Is.EqualTo(SkillLevels.Basic));
            Assert.That(copy.CompletedTrainings, Does.Contain("FishSkillConstruction:2"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ManualStudy_BlockedBeforeConfirm()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var skills = entMan.System<SkillSystem>();
            var mob = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            skills.EnsureSkillset(mob, "StationEngineer");

            var skillset = entMan.GetComponent<SkillsetComponent>(mob);
            skillset.Skills["FishSkillConstruction"] = SkillLevels.Unskilled;
            Assert.That(skillset.Locked, Is.False);

            var manual = entMan.SpawnEntity("FishSkillManualConstructionBasic", MapCoordinates.Nullspace);
            var manualComp = entMan.GetComponent<SkillTrainingManualComponent>(manual);
            Assert.That(skills.TryStartManualStudy(mob, (manual, manualComp)), Is.False);

            entMan.DeleteEntity(manual);
            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }
}
