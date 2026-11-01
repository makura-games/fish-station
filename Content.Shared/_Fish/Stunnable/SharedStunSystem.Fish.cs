using Content.Shared._Fish.Crit;
using Content.Shared.Mobs.Components;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Stunnable;

/// <summary>
/// Хуки soft-crit для ванильного knockdown: одна проверка вместо копий в SharedStunSystem.Knockdown.
/// </summary>
public abstract partial class SharedStunSystem
{
    [Dependency] private EntityQuery<MobStateComponent> _fishMobStateQuery = default!;

    /// <summary>
    /// Персонаж в soft-crit: ручное вставание, без AutoStand-спама.
    /// </summary>
    private bool IsFishSoftCrit(EntityUid uid)
    {
        return _fishMobStateQuery.TryComp(uid, out var mobState) &&
               SharedFishComplexCritSystem.AllowsSoftCritActions(EntityManager, uid, mobState.CurrentState);
    }
}
#pragma warning restore IDE0130
