#nullable enable
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
    private static readonly ProtoId<JobSkillBudgetPrototype> EngineerBudgetId = "FishStationEngineer";
    private static readonly ProtoId<JobSkillBudgetPrototype> ErtSecurityBudgetId = "FishERTSecurity";
    private static readonly ProtoId<JobPrototype> StationEngineerJob = "StationEngineer";
    private static readonly ProtoId<JobPrototype> ErtSecurityJob = "ERTSecurity";
    private static readonly ProtoId<SkillPrototype> ConstructionSkill = "FishSkillConstruction";
    private static readonly ProtoId<SkillPrototype> MedicalSkill = "FishSkillMedical";
    private static readonly ProtoId<SkillPrototype> WeaponsSkill = "FishSkillWeapons";

    [Test]
    public async Task JobBudget_GrantsMinSkillsAndPoints()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var proto = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            Assert.That(proto.TryIndex(EngineerBudgetId, out var engineerBudget), Is.True);
            Assert.That(engineerBudget!.GetJobId(), Is.EqualTo(StationEngineerJob));

            var mob = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var skills = entMan.System<SkillSystem>();
            skills.EnsureSkillset(mob, StationEngineerJob);

            Assert.That(entMan.TryGetComponent(mob, out SkillsetComponent? skillset), Is.True);
            Assert.That(skillset!.BudgetGranted, Is.True);
            Assert.That(skillset.JobId, Is.EqualTo(StationEngineerJob));
            Assert.That(skillset.TotalPoints, Is.EqualTo(engineerBudget.SkillPoints));
            Assert.That(skillset.RemainingPoints, Is.EqualTo(engineerBudget.SkillPoints));
            Assert.That(skillset.Locked, Is.False);
            Assert.That(skillset.RemainingPoints, Is.GreaterThan(0));

            foreach (var (skill, min) in engineerBudget.MinSkills)
            {
                Assert.That(skills.GetSkillValue(mob, skill), Is.EqualTo(min),
                    $"Минимум роли для {skill}");
            }

            // До Confirm provisional-уровни не должны влиять на эффекты.
            var jobMinConstruction = skills.GetMinSkill(StationEngineerJob, ConstructionSkill);
            skills.ForceSetSkillLevel(mob, ConstructionSkill, SkillLevels.Master);
            Assert.That(skills.GetEffectiveSkillValue(mob, ConstructionSkill), Is.EqualTo(jobMinConstruction));

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
            Assert.That(proto.TryIndex(ErtSecurityBudgetId, out var budget), Is.True);
            Assert.That(budget!.SkillPoints, Is.EqualTo(0));

            var skills = entMan.System<SkillSystem>();
            var mob = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            skills.EnsureSkillset(mob, ErtSecurityJob);

            Assert.That(entMan.TryGetComponent(mob, out SkillsetComponent? skillset), Is.True);
            Assert.That(skillset!.Locked, Is.True);
            Assert.That(skillset.RemainingPoints, Is.EqualTo(0));
            Assert.That(skills.GetEffectiveSkillValue(mob, WeaponsSkill),
                Is.EqualTo(budget.MinSkills[WeaponsSkill]));

            // Без skillset — Baseline (госты/нюкеры без job).
            var bare = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            Assert.That(skills.GetEffectiveSkillValue(bare, ConstructionSkill), Is.EqualTo(SkillLevels.Baseline));

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
            skills.EnsureSkillset(mob, StationEngineerJob);
            skills.ForceSetSkillLevel(mob, ConstructionSkill, SkillLevels.Unskilled);
            skills.ForceSetLocked(mob, true);

            manual = entMan.SpawnEntity("FishSkillManualConstructionBasic", MapCoordinates.Nullspace);
            skills.ForceSetManualStudyTime(manual, timing.TickPeriod);
            Assert.That(skills.TryStartManualStudy(mob, (manual, entMan.GetComponent<SkillTrainingManualComponent>(manual))), Is.True);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var skills = entMan.System<SkillSystem>();
            Assert.That(skills.GetSkillValue(mob, ConstructionSkill), Is.EqualTo(SkillLevels.Basic));
            Assert.That(skills.GetEffectiveSkillValue(mob, ConstructionSkill), Is.EqualTo(SkillLevels.Basic));
            Assert.That(
                entMan.GetComponent<SkillsetComponent>(mob).CompletedTrainings,
                Does.Contain(SkillMath.MakeTrainingKey(ConstructionSkill, SkillLevels.Basic)));

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
        var entMan = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var seri = server.ResolveDependency<ISerializationManager>();
            var skills = entMan.System<SkillSystem>();
            var mob = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            skills.EnsureSkillset(mob, StationEngineerJob);
            skills.ForceSetSkillLevel(mob, ConstructionSkill, SkillLevels.Trained);
            skills.ForceSetSkillLevel(mob, MedicalSkill, SkillLevels.Basic);
            skills.ForceSetLocked(mob, true);

            var original = entMan.GetComponent<SkillsetComponent>(mob);
            // CompletedTrainings пишется только через обучение — проверяем round-trip остальных полей + Skills.
            var node = seri.WriteValue(original, alwaysWrite: true, notNullableOverride: true);
            Assert.That(node, Is.InstanceOf<MappingDataNode>());

            var copy = seri.Read<SkillsetComponent>(node, notNullableOverride: true);
            Assert.That(copy.RemainingPoints, Is.EqualTo(original.RemainingPoints));
            Assert.That(copy.TotalPoints, Is.EqualTo(original.TotalPoints));
            Assert.That(copy.JobId, Is.EqualTo(original.JobId));
            Assert.That(copy.BudgetGranted, Is.EqualTo(original.BudgetGranted));
            Assert.That(copy.Locked, Is.EqualTo(original.Locked));
            Assert.That(skills.GetSkillValue(mob, ConstructionSkill), Is.EqualTo(SkillLevels.Trained));
            Assert.That(copy.Skills.TryGetValue(ConstructionSkill, out var construction) && construction == SkillLevels.Trained, Is.True);
            Assert.That(copy.Skills.TryGetValue(MedicalSkill, out var medical) && medical == SkillLevels.Basic, Is.True);

            entMan.DeleteEntity(mob);
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
            skills.EnsureSkillset(mob, StationEngineerJob);
            skills.ForceSetSkillLevel(mob, ConstructionSkill, SkillLevels.Unskilled);

            Assert.That(entMan.GetComponent<SkillsetComponent>(mob).Locked, Is.False);

            var manual = entMan.SpawnEntity("FishSkillManualConstructionBasic", MapCoordinates.Nullspace);
            var manualComp = entMan.GetComponent<SkillTrainingManualComponent>(manual);
            Assert.That(skills.TryStartManualStudy(mob, (manual, manualComp)), Is.False);

            entMan.DeleteEntity(manual);
            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }
}
