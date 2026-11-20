#nullable enable
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.HTN.PrimitiveTasks.Operators;
using Content.Server._Fish.NPC.HTN;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.NPC;

[TestFixture]
public sealed class NpcRangedCombatTests
{
    private static readonly ProtoId<HTNCompoundPrototype> GunCombatId = "GunCombatCompound";
    private static readonly ProtoId<HTNCompoundPrototype> RangedCombatId = "RangedCombatCompound";
    private static readonly ProtoId<HTNCompoundPrototype> XenoId = "XenoCompound";
    private static readonly ProtoId<HTNCompoundPrototype> PickupAmmoId = "PickupAmmoCompound";
    private static readonly ProtoId<HTNCompoundPrototype> SimpleHostileId = "SimpleHostileCompound";
    private static readonly ProtoId<HTNCompoundPrototype> MeleeCombatId = "MeleeCombatCompound";

    /// <summary>
    /// GunCombat подходит к цели на RangedRange, а не MeleeRange.
    /// </summary>
    [Test]
    public async Task GunCombatCompoundUsesRangedRange()
    {
        await using var pair = await PoolManager.GetServerClient();
        var compound = pair.Server.ProtoMan.Index(GunCombatId);

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
        var compound = pair.Server.ProtoMan.Index(RangedCombatId);

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
        var compound = pair.Server.ProtoMan.Index(XenoId);

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

    private static bool HasCompoundTask(HTNCompoundPrototype compound, ProtoId<HTNCompoundPrototype> taskId)
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
