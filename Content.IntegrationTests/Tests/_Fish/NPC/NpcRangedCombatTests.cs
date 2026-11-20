#nullable enable
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.HTN.PrimitiveTasks.Operators;
using Content.Server._Fish.NPC.HTN;

namespace Content.IntegrationTests.Tests._Fish.NPC;

[TestFixture]
public sealed class NpcRangedCombatTests
{
    // Строки вместо ProtoId: YAMLLinter не знает kind HTNCompoundPrototype в IntegrationTests.
    private const string GunCombatId = "GunCombatCompound";
    private const string RangedCombatId = "RangedCombatCompound";
    private const string XenoId = "XenoCompound";
    private const string PickupAmmoId = "PickupAmmoCompound";
    private const string SimpleHostileId = "SimpleHostileCompound";
    private const string MeleeCombatId = "MeleeCombatCompound";

    /// <summary>
    /// GunCombat подходит к цели на RangedRange, а не MeleeRange.
    /// </summary>
    [Test]
    public async Task GunCombatCompoundUsesRangedRange()
    {
        await using var pair = await PoolManager.GetServerClient();
        var compound = pair.Server.ProtoMan.Index<HTNCompoundPrototype>(GunCombatId);

        Assert.That(compound.Branches, Is.Not.Empty);
        var move = FindMoveTo(compound);
        Assert.That(move, Is.Not.Null, "GunCombatCompound без MoveToOperator");
        Assert.That(move!.RangeKey, Is.EqualTo("RangedRange"));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// RangedCombat при пустом магазине умеет ReloadGunOperator и PickupAmmo.
    /// </summary>
    [Test]
    public async Task RangedCombatCompoundHasReloadAndPickupAmmo()
    {
        await using var pair = await PoolManager.GetServerClient();
        var compound = pair.Server.ProtoMan.Index<HTNCompoundPrototype>(RangedCombatId);

        Assert.That(HasOperator<ReloadGunOperator>(compound), Is.True, "Нет ReloadGunOperator");
        Assert.That(HasCompoundTask(compound, PickupAmmoId), Is.True, "Нет PickupAmmoCompound");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// XenoCompound больше не дублирует melee/idle, а делегирует SimpleHostile.
    /// </summary>
    [Test]
    public async Task XenoCompoundDelegatesToSimpleHostile()
    {
        await using var pair = await PoolManager.GetServerClient();
        var compound = pair.Server.ProtoMan.Index<HTNCompoundPrototype>(XenoId);

        Assert.That(HasCompoundTask(compound, SimpleHostileId), Is.True);
        Assert.That(HasCompoundTask(compound, MeleeCombatId), Is.False);

        await pair.CleanReturnAsync();
    }

    private static MoveToOperator? FindMoveTo(HTNCompoundPrototype compound)
    {
        foreach (var branch in compound.Branches)
        {
            foreach (var task in branch.Tasks)
            {
                if (task is HTNPrimitiveTask primitive && primitive.Operator is MoveToOperator move)
                    return move;
            }
        }

        return null;
    }

    private static bool HasOperator<T>(HTNCompoundPrototype compound) where T : HTNOperator
    {
        foreach (var branch in compound.Branches)
        {
            foreach (var task in branch.Tasks)
            {
                if (task is HTNPrimitiveTask primitive && primitive.Operator is T)
                    return true;
            }
        }

        return false;
    }

    private static bool HasCompoundTask(HTNCompoundPrototype compound, string taskId)
    {
        foreach (var branch in compound.Branches)
        {
            foreach (var task in branch.Tasks)
            {
                if (task is HTNCompoundTask compoundTask && compoundTask.Task == taskId)
                    return true;
            }
        }

        return false;
    }
}
