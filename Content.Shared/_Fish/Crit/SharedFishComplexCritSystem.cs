using Content.Shared._Fish.Reactions;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Sunrise.Eye;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable;
using Robust.Shared.Timing;
using MobStateEnum = Content.Shared.Mobs.MobState;

namespace Content.Shared._Fish.Crit;

/// <summary>
/// Shared API и soft-crit: ползание медленнее, вставание долгое (не сбрасывать прогресс).
/// </summary>
public abstract partial class SharedFishComplexCritSystem : EntitySystem
{
    [Dependency] protected DamageableSystem Damageable = default!;
    [Dependency] protected MobStateSystem MobStateSys = default!;
    [Dependency] protected MobThresholdSystem MobThreshold = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedDarkenedVisionSystem _darkenedVision = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedWieldableSystem _wieldable = default!;
    [Dependency] private SharedPopupSystem _critPopup = default!;

    private const float CritVisionDarkening = 3.5f;
    private const float DeepCritVisionDarkening = 6f;

    /// <summary>Базовый множитель в Crit (поверх crawl ~0.6 → ~0.13; травма замедляет ещё).</summary>
    public const float CritCrawlSpeedMultiplier = 0.22f; // Fish-edit: медленное ползание; травма замедляет ещё

    /// <summary>Вставание в Crit в N раз дольше.</summary>
    public const float CritStandTimeMultiplier = 3.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishComplexCritComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FishComplexCritComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FishComplexCritComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FishComplexCritComponent, GetVisionDarkeningEvent>(OnGetVisionDarkening);
        SubscribeLocalEvent<FishComplexCritComponent, GetStandUpTimeEvent>(OnGetStandUpTime);
        SubscribeLocalEvent<FishComplexCritComponent, StandUpAttemptEvent>(OnStandUpAttempt);
        SubscribeLocalEvent<FishComplexCritComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        // Soft-crit: максимум одна рука — без двуручного хвата / двуручного огнестрела.
        SubscribeLocalEvent<FishComplexCritComponent, WieldAttemptEvent>(OnSoftCritWieldAttempt);
        SubscribeLocalEvent<FishComplexCritComponent, ShotAttemptedEvent>(OnSoftCritShotAttempt);
    }

    private void OnMapInit(Entity<FishComplexCritComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<MobThresholdsComponent>(ent, out var thresholds))
        {
            if (MobThreshold.TryGetThresholdForState(ent, MobStateEnum.Critical, out var crit, thresholds) &&
                crit != null)
            {
                ent.Comp.CritDamageThreshold = crit.Value.Float();
            }

            if (MobThreshold.TryGetThresholdForState(ent, MobStateEnum.Dead, out var dead, thresholds) &&
                dead != null)
            {
                ent.Comp.OriginalDeadThreshold = dead.Value.Float();
                ent.Comp.DeadDamageThreshold = dead.Value.Float();
            }
        }

        ent.Comp.NextTick = Timing.CurTime + ent.Comp.TickInterval;
        Dirty(ent);
    }

    private void OnShutdown(Entity<FishComplexCritComponent> ent, ref ComponentShutdown args)
    {
        RemCompDeferred<FishCritShockComponent>(ent);
        RemCompDeferred<FishHeartFailureComponent>(ent);
        RemCompDeferred<FishCardiacArrestComponent>(ent);
        RemCompDeferred<FishLoseBreathComponent>(ent);
        RemCompDeferred<FishCritUnconsciousComponent>(ent);
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnMobStateChanged(Entity<FishComplexCritComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobStateEnum.Critical || args.OldMobState == MobStateEnum.Critical)
        {
            _darkenedVision.UpdateVisionDarkening(ent.Owner);
            _movement.RefreshMovementSpeedModifiers(ent.Owner);
        }

        if (args.NewMobState != MobStateEnum.Critical)
            return;

        if (args.OldMobState != MobStateEnum.Critical && Timing.IsFirstTimePredicted)
        {
            _jitter.DoJitter(ent, TimeSpan.FromSeconds(2.5), refresh: true, amplitude: 12f, frequency: 5f);
            // Server override — один Subscribe на MobStateChanged (без дубля в FishComplexCritSystem).
            PlayCritFallSound(ent);
        }

        // Снять двуручный хват при входе в Crit.
        _wieldable.UnwieldAll(ent.Owner, force: true);

        if (SharedFishCharacterReactionsSystem.ShouldSuppressSoftCritStand(EntityManager, ent.Owner))
            return;

        _blocker.UpdateCanMove(ent.Owner);
    }

    private void OnSoftCritWieldAttempt(Entity<FishComplexCritComponent> ent, ref WieldAttemptEvent args)
    {
        if (!MobStateSys.IsCritical(ent))
            return;

        if (!AllowsSoftCritActions(EntityManager, ent.Owner, MobStateEnum.Critical))
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString("fish-crit-one-hand-only");
    }

    private void OnSoftCritShotAttempt(Entity<FishComplexCritComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!MobStateSys.IsCritical(ent))
            return;

        if (!AllowsSoftCritActions(EntityManager, ent.Owner, MobStateEnum.Critical))
            return;

        // Только оружие, которое обязано стрелять с двуручного хвата.
        if (!HasComp<GunRequiresWieldComponent>(args.Used.Owner))
            return;

        args.Cancel();
        if (Timing.IsFirstTimePredicted)
            _critPopup.PopupCursor(Loc.GetString("fish-crit-one-hand-gun"), ent.Owner, PopupType.SmallCaution);
    }

    private void OnGetVisionDarkening(Entity<FishComplexCritComponent> ent, ref GetVisionDarkeningEvent args)
    {
        if (!MobStateSys.IsCritical(ent))
            return;

        var health = GetCritScaleHealth(ent);
        args.Strength += health <= -50f ? DeepCritVisionDarkening : CritVisionDarkening;
    }

    /// <summary>
    /// Soft-crit: вставать можно, но долго. Не отменяем StandAttempt —
    /// иначе do-after «встать» завершается в никуда / прогресс сбрасывается.
    /// </summary>
    private void OnGetStandUpTime(Entity<FishComplexCritComponent> ent, ref GetStandUpTimeEvent args)
    {
        if (!MobStateSys.IsCritical(ent))
            return;

        args.DoAfterTime *= CritStandTimeMultiplier;
    }

    private void OnStandUpAttempt(Entity<FishComplexCritComponent> ent, ref StandUpAttemptEvent args)
    {
        if (!MobStateSys.IsCritical(ent))
            return;

        if (HasComp<FishCritUnconsciousComponent>(ent))
        {
            args.Cancelled = true;
            args.Message = (Loc.GetString("fish-crit-cant-stand-unconscious"), PopupType.SmallCaution);
            return;
        }

        // Не спамим popup на каждый AutoStand-тик — только ручная попытка.
        if (!args.Autostand)
            args.Message = (Loc.GetString("fish-crit-stand-struggle"), PopupType.Small);
    }

    private void OnRefreshSpeed(Entity<FishComplexCritComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!MobStateSys.IsCritical(ent))
            return;

        // Медленное ползание в Crit.
        args.ModifySpeed(CritCrawlSpeedMultiplier);
    }

    protected void RefreshCritVision(EntityUid uid)
    {
        _darkenedVision.UpdateVisionDarkening(uid);
    }

    /// <summary>Звук падения в Crit — только на сервере (см. FishComplexCritSystem).</summary>
    protected virtual void PlayCritFallSound(Entity<FishComplexCritComponent> ent) { }

    public float GetCritScaleHealth(EntityUid uid, FishComplexCritComponent? crit = null, DamageableComponent? damageable = null)
    {
        if (!Resolve(uid, ref crit, false) || !Resolve(uid, ref damageable, false))
            return 100f;

#pragma warning disable CS0618
        var total = Damageable.GetTotalDamage((uid, damageable));
#pragma warning restore CS0618
        return crit.CritDamageThreshold - (float)total;
    }

    public bool IsInCritZone(EntityUid uid, FishComplexCritComponent? crit = null, DamageableComponent? damageable = null)
    {
        if (!Resolve(uid, ref crit, false) || !Resolve(uid, ref damageable, false))
            return false;

        if (MobStateSys.IsDead(uid))
            return false;

#pragma warning disable CS0618
        return (float)Damageable.GetTotalDamage((uid, damageable)) >= crit.CritDamageThreshold;
#pragma warning restore CS0618
    }

    public static bool AllowsSoftCritActions(IEntityManager entMan, EntityUid uid, MobStateEnum state)
    {
        if (state != MobStateEnum.Critical)
            return false;

        if (!entMan.HasComponent<FishComplexCritComponent>(uid))
            return false;

        if (SharedFishCharacterReactionsSystem.ShouldSuppressSoftCritStand(entMan, uid))
            return false;

        return true;
    }

    public void AdjustLoseBreath(EntityUid uid, TimeSpan amount, TimeSpan? max = null)
    {
        var lose = EnsureComp<FishLoseBreathComponent>(uid);
        var cap = max ?? lose.MaxRemaining;
        var next = lose.Remaining + amount;
        if (next < TimeSpan.Zero)
            next = TimeSpan.Zero;
        if (next > cap)
            next = cap;
        lose.Remaining = next;
        lose.MaxRemaining = cap;
        Dirty(uid, lose);

        if (lose.Remaining <= TimeSpan.Zero)
            RemCompDeferred<FishLoseBreathComponent>(uid);
    }

    public void SetLoseBreath(EntityUid uid, TimeSpan amount)
    {
        if (amount <= TimeSpan.Zero)
        {
            RemCompDeferred<FishLoseBreathComponent>(uid);
            return;
        }

        var lose = EnsureComp<FishLoseBreathComponent>(uid);
        lose.Remaining = amount > lose.MaxRemaining ? lose.MaxRemaining : amount;
    }

    public bool TrySetHeartAttack(EntityUid uid, bool arrest)
    {
        if (arrest)
        {
            var comp = EnsureComp<FishCardiacArrestComponent>(uid);
            comp.Active = true;
            Dirty(uid, comp);
            return true;
        }

        if (!TryComp<FishCardiacArrestComponent>(uid, out _))
            return false;

        RemCompDeferred<FishCardiacArrestComponent>(uid);
        return true;
    }

    public bool IsUndergoingCardiacArrest(EntityUid uid)
    {
        return TryComp<FishCardiacArrestComponent>(uid, out var arrest) && arrest.Active;
    }

    /// <summary>Идёт do-after вставания — не рвём его knockdown/oxy.</summary>
    public static bool IsTryingToStand(IEntityManager entMan, EntityUid uid)
    {
        return entMan.TryGetComponent<KnockedDownComponent>(uid, out var kd) && kd.DoAfterId != null;
    }
}
