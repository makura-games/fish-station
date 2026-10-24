using Content.Shared._Fish.Crit;
using Content.Shared.Atmos.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Robust.Shared.Timing;
using MobStateEnum = Content.Shared.Mobs.MobState;

namespace Content.Shared._Fish.Reactions;

/// <summary>
/// Shared-координатор Character Reactions: Crit / огонь / очистка состояний.
/// </summary>
public abstract partial class SharedFishCharacterReactionsSystem : EntitySystem
{
    [Dependency] protected MobStateSystem MobStateSys = default!;
    [Dependency] protected StandingStateSystem Standing = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected SharedFishTraumaSystem TraumaSys = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishCharacterReactionsComponent, MapInitEvent>(OnReactionsMapInit);
        SubscribeLocalEvent<FishCharacterReactionsComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FishCharacterReactionsComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnReactionsMapInit(Entity<FishCharacterReactionsComponent> ent, ref MapInitEvent args)
    {
        // Единый хаб травмы на всех organic с Reactions.
        EnsureComp<FishTraumaStatusComponent>(ent.Owner);
    }

    private void OnMobStateChanged(Entity<FishCharacterReactionsComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobStateEnum.Dead)
        {
            ClearTransientReactionState(ent.Owner);
            return;
        }

        // После revive из Dead — убрать зависшие осложнения Crit/реакций.
        if (args.OldMobState == MobStateEnum.Dead && args.NewMobState != MobStateEnum.Dead)
        {
            RemCompDeferred<FishCritUnconsciousComponent>(ent);
            RemCompDeferred<FishLoseBreathComponent>(ent);
        }
    }

    private void OnShutdown(Entity<FishCharacterReactionsComponent> ent, ref ComponentShutdown args)
    {
        ClearTransientReactionState(ent.Owner);
    }

    /// <summary>
    /// Временные FX, которые не должны переживать смерть / удаление сущности.
    /// </summary>
    public void ClearTransientReactionState(EntityUid uid)
    {
        RemCompDeferred<FishCritUnconsciousComponent>(uid);
        RemCompDeferred<FishLoseBreathComponent>(uid);
        RemCompDeferred<FishCritShockComponent>(uid);
        RemCompDeferred<FishBlastedComponent>(uid);
        // ClearTrauma также RefreshMovementSpeed / vision — ручной сброс полей этого не делал.
        if (TryComp<FishTraumaStatusComponent>(uid, out var trauma))
            TraumaSys.ClearTrauma((uid, trauma));

        if (MobStateSys.IsDead(uid))
        {
            RemCompDeferred<FishHeartFailureComponent>(uid);
            RemCompDeferred<FishCardiacArrestComponent>(uid);
        }
    }

    /// <summary>
    /// Soft-crit crawl: не поднимаем горящего / бессознательного.
    /// Knockdown больше не блокирует soft-crit — персонаж ползает, а не встаёт рывками.
    /// Relies on FlammableComponent.OnFire being AutoNetworkedField (client-visible).
    /// </summary>
    public static bool ShouldSuppressSoftCritStand(IEntityManager entMan, EntityUid uid)
    {
        if (entMan.TryGetComponent<FlammableComponent>(uid, out var flam) && flam.OnFire)
            return true;

        if (entMan.HasComponent<FishCritUnconsciousComponent>(uid))
            return true;

        return false;
    }
}
