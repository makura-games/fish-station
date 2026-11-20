#nullable enable
using Content.Server._Fish.Skills;
using Content.Shared._Fish.Skills;
using Content.Shared.DoAfter;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
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

        EntityUid mob = default;
        JobSkillBudgetPrototype? engineerBudget = null;

        await server.WaitPost(() =>
        {
            Assert.That(proto.TryIndex(EngineerBudgetId, out engineerBudget), Is.True);
            Assert.That(engineerBudget!.GetJobId(), Is.EqualTo(StationEngineerJob));

            mob = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            entMan.System<SkillSystem>().EnsureSkillset(mob, StationEngineerJob);
        });

        await server.WaitAssertion(() =>
        {
            var skills = entMan.System<SkillSystem>();
            Assert.That(entMan.TryGetComponent(mob, out SkillsetComponent? skillset), Is.True);
            Assert.That(skillset!.BudgetGranted, Is.True);
            Assert.That(skillset.JobId, Is.EqualTo(StationEngineerJob));
            Assert.That(skillset.TotalPoints, Is.EqualTo(engineerBudget!.SkillPoints));
            Assert.That(skillset.RemainingPoints, Is.EqualTo(engineerBudget.SkillPoints));
            Assert.That(skillset.Locked, Is.False);
            Assert.That(skillset.RemainingPoints, Is.GreaterThan(0));

            foreach (var (skill, min) in engineerBudget.MinSkills)
            {
                Assert.That(skills.GetSkillValue(mob, skill), Is.EqualTo(min),
                    $"Минимум роли для {skill}");
            }
        });

        await server.WaitPost(() =>
        {
            var skills = entMan.System<SkillSystem>();
            // Эффекты учитывают уровни сразу — Confirm только закрепляет очки.
            skills.ForceSetSkillLevel(mob, ConstructionSkill, SkillLevels.Master);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(
                entMan.System<SkillSystem>().GetEffectiveSkillValue(mob, ConstructionSkill),
                Is.EqualTo(SkillLevels.Master));
        });

        await server.WaitPost(() => entMan.DeleteEntity(mob));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ZeroPointBudget_AutoLocksWithMins()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var proto = server.ProtoMan;

        EntityUid mob = default;
        EntityUid bare = default;
        JobSkillBudgetPrototype? budget = null;

        await server.WaitPost(() =>
        {
            Assert.That(proto.TryIndex(ErtSecurityBudgetId, out budget), Is.True);
            Assert.That(budget!.SkillPoints, Is.EqualTo(0));

            var skills = entMan.System<SkillSystem>();
            mob = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            skills.EnsureSkillset(mob, ErtSecurityJob);
            bare = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
        });

        await server.WaitAssertion(() =>
        {
            var skills = entMan.System<SkillSystem>();
            Assert.That(entMan.TryGetComponent(mob, out SkillsetComponent? skillset), Is.True);
            Assert.That(skillset!.Locked, Is.True);
            Assert.That(skillset.RemainingPoints, Is.EqualTo(0));
            Assert.That(skills.GetEffectiveSkillValue(mob, WeaponsSkill),
                Is.EqualTo(budget!.MinSkills[WeaponsSkill]));

            // Без skillset — Baseline (госты/нюкеры без job).
            Assert.That(skills.GetEffectiveSkillValue(bare, ConstructionSkill), Is.EqualTo(SkillLevels.Baseline));
        });

        await server.WaitPost(() =>
        {
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
        });

        await server.WaitPost(() =>
        {
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

        EntityUid mob = default;
        DataNode? node = null;
        SkillsetComponent? original = null;

        await server.WaitPost(() =>
        {
            var seri = server.ResolveDependency<ISerializationManager>();
            var skills = entMan.System<SkillSystem>();
            mob = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            skills.EnsureSkillset(mob, StationEngineerJob);
            skills.ForceSetSkillLevel(mob, ConstructionSkill, SkillLevels.Trained);
            skills.ForceSetSkillLevel(mob, MedicalSkill, SkillLevels.Basic);
            skills.ForceSetLocked(mob, true);

            original = entMan.GetComponent<SkillsetComponent>(mob);
            node = seri.WriteValue(original, alwaysWrite: true, notNullableOverride: true);
        });

        await server.WaitAssertion(() =>
        {
            var seri = server.ResolveDependency<ISerializationManager>();
            var skills = entMan.System<SkillSystem>();
            Assert.That(node, Is.InstanceOf<MappingDataNode>());
            Assert.That(original, Is.Not.Null);

            var copy = seri.Read<SkillsetComponent>(node!, notNullableOverride: true);
            Assert.That(copy.RemainingPoints, Is.EqualTo(original!.RemainingPoints));
            Assert.That(copy.TotalPoints, Is.EqualTo(original.TotalPoints));
            Assert.That(copy.JobId, Is.EqualTo(original.JobId));
            Assert.That(copy.BudgetGranted, Is.EqualTo(original.BudgetGranted));
            Assert.That(copy.Locked, Is.EqualTo(original.Locked));
            Assert.That(skills.GetSkillValue(mob, ConstructionSkill), Is.EqualTo(SkillLevels.Trained));
            Assert.That(copy.Skills.TryGetValue(ConstructionSkill, out var construction) && construction == SkillLevels.Trained, Is.True);
            Assert.That(copy.Skills.TryGetValue(MedicalSkill, out var medical) && medical == SkillLevels.Basic, Is.True);
        });

        await server.WaitPost(() => entMan.DeleteEntity(mob));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ManualStudy_BlockedBeforeConfirm()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        EntityUid mob = default;
        EntityUid manual = default;

        await server.WaitPost(() =>
        {
            var skills = entMan.System<SkillSystem>();
            mob = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            skills.EnsureSkillset(mob, StationEngineerJob);
            skills.ForceSetSkillLevel(mob, ConstructionSkill, SkillLevels.Unskilled);
            manual = entMan.SpawnEntity("FishSkillManualConstructionBasic", MapCoordinates.Nullspace);
        });

        await server.WaitAssertion(() =>
        {
            var skills = entMan.System<SkillSystem>();
            Assert.That(entMan.GetComponent<SkillsetComponent>(mob).Locked, Is.False);
            var manualComp = entMan.GetComponent<SkillTrainingManualComponent>(manual);
            Assert.That(skills.TryStartManualStudy(mob, (manual, manualComp)), Is.False);
        });

        await server.WaitPost(() =>
        {
            entMan.DeleteEntity(manual);
            entMan.DeleteEntity(mob);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HumanSkillFloor_AppliesToSkillsWithoutJobMin()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        EntityUid mob = default;

        await server.WaitPost(() =>
        {
            mob = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            entMan.System<SkillSystem>().EnsureSkillset(mob, StationEngineerJob);
        });

        await server.WaitAssertion(() =>
        {
            var skills = entMan.System<SkillSystem>();
            // Медицина не в minSkills инженера — должен быть HumanSkillFloor (Baseline).
            Assert.That(skills.GetMinSkill(StationEngineerJob, MedicalSkill),
                Is.EqualTo(SharedSkillSystem.HumanSkillFloor));
            Assert.That(skills.GetSkillValue(mob, MedicalSkill),
                Is.EqualTo(SharedSkillSystem.HumanSkillFloor));
            Assert.That(skills.GetEffectiveSkillValue(mob, MedicalSkill),
                Is.EqualTo(SharedSkillSystem.HumanSkillFloor));
        });

        await server.WaitPost(() => entMan.DeleteEntity(mob));
        await pair.CleanReturnAsync();
    }
}
