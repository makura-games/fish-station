using Content.Shared._Fish.Crit;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Sunrise.Eye;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using MobStateEnum = Content.Shared.Mobs.MobState;

namespace Content.Shared._Fish.Reactions;

/// <summary>
/// Физиологические ограничения от травм: ползание, вставание, зрение.
/// Источники: Merck/CDC blast injury + ATLS геморрагический шок (игровой адапт).
/// </summary>
public abstract partial class SharedFishTraumaSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedDarkenedVisionSystem _darkenedVision = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedFishComplexCritSystem _crit = default!;
    [Dependency] private EntityQuery<BloodstreamComponent> _bloodQuery = default!;

    /// <summary>ATLS class III (~30% крови) — вставать крайне тяжело.</summary>
    public const float ShockStandBlock = 0.55f;

    /// <summary>Затухание травм и пересчёт шока не чаще раза в секунду.</summary>
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    /// <summary>Сильная хромота — не встать без помощи.</summary>
    public const float LegStandBlock = 0.7f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishTraumaStatusComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FishTraumaStatusComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FishTraumaStatusComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<FishTraumaStatusComponent, StandUpAttemptEvent>(OnStandUpAttempt);
        SubscribeLocalEvent<FishTraumaStatusComponent, GetStandUpTimeEvent>(OnGetStandUpTime);
        SubscribeLocalEvent<FishTraumaStatusComponent, GetVisionDarkeningEvent>(OnVisionDarkening);
        SubscribeLocalEvent<FishTraumaStatusComponent, MobStateChangedEvent>(OnMobState);
        SubscribeLocalEvent<FishTraumaStatusComponent, DamageChangedEvent>(OnDamageChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var cur = _timing.CurTime;
        var query = EntityQueryEnumerator<FishTraumaStatusComponent>();
        while (query.MoveNext(out var uid, out var trauma))
        {
            if (trauma.NextUpdate > cur)
                continue;

            trauma.NextUpdate = cur + UpdateInterval;

            var dirty = false;

            if (trauma.ConcussionUntil != TimeSpan.Zero && cur >= trauma.ConcussionUntil)
            {
                trauma.ConcussionUntil = TimeSpan.Zero;
                trauma.ConcussionSeverity = 0f;
                dirty = true;
            }

            if (trauma.TinnitusUntil != TimeSpan.Zero && cur >= trauma.TinnitusUntil)
            {
                trauma.TinnitusUntil = TimeSpan.Zero;
                dirty = true;
            }

            if (trauma.DisorientUntil != TimeSpan.Zero && cur >= trauma.DisorientUntil)
            {
                trauma.DisorientUntil = TimeSpan.Zero;
                dirty = true;
            }

            if (trauma.ChestTraumaUntil != TimeSpan.Zero && cur >= trauma.ChestTraumaUntil)
            {
                trauma.ChestTraumaUntil = TimeSpan.Zero;
                trauma.ChestTraumaSeverity = 0f;
                dirty = true;
            }

            // Ноги медленно восстанавливаются без лечения (игровой компромисс).
            if (trauma.LegImpairment > 0f)
            {
                var step = (float)UpdateInterval.TotalSeconds;
                trauma.LegImpairment = Math.Max(0f, trauma.LegImpairment - step * 0.015f);
                dirty = true;
            }

            var shock = CalculateShockFactor(uid);
            if (Math.Abs(shock - trauma.ShockFactor) > 0.02f)
            {
                trauma.ShockFactor = shock;
                dirty = true;
            }

            if (dirty)
            {
                Dirty(uid, trauma);
                _movement.RefreshMovementSpeedModifiers(uid);
                _darkenedVision.UpdateVisionDarkening(uid);
            }
        }
    }

    private void OnStartup(Entity<FishTraumaStatusComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.ShockFactor = CalculateShockFactor(ent);
        Dirty(ent);
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnShutdown(Entity<FishTraumaStatusComponent> ent, ref ComponentShutdown args)
    {
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
        _darkenedVision.UpdateVisionDarkening(ent.Owner);
    }

    private void OnMobState(Entity<FishTraumaStatusComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobStateEnum.Dead)
            ClearTrauma(ent);
    }

    /// <summary>
    /// Глубина Crit меняет GetMovementMultiplier — без refresh скорость отстаёт от урона.
    /// </summary>
    private void OnDamageChanged(Entity<FishTraumaStatusComponent> ent, ref DamageChangedEvent args)
    {
        if (!_mobState.IsCritical(ent) || !HasComp<FishComplexCritComponent>(ent))
            return;

        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnRefreshSpeed(Entity<FishTraumaStatusComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var mult = GetMovementMultiplier(ent);
        if (mult < 0.999f)
            args.ModifySpeed(mult);
    }

    private void OnStandUpAttempt(Entity<FishTraumaStatusComponent> ent, ref StandUpAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!CanAttemptStand(ent, out var reason))
        {
            args.Cancelled = true;
            // Не спамим popup на AutoStand-тик.
            if (reason != null && !args.Autostand)
                args.Message = (Loc.GetString(reason), PopupType.SmallCaution);
        }
    }

    private void OnGetStandUpTime(Entity<FishTraumaStatusComponent> ent, ref GetStandUpTimeEvent args)
    {
        // Слабость / контузия / грудь — дольше подниматься (поверх Crit 3.5×).
        var extra = 1f
                    + ent.Comp.ShockFactor * 1.5f
                    + ent.Comp.LegImpairment * 2f
                    + ent.Comp.ConcussionSeverity * 1.2f
                    + ent.Comp.ChestTraumaSeverity * 1.5f;

        if (_timing.CurTime < ent.Comp.DisorientUntil)
            extra += 0.8f;

        args.DoAfterTime *= Math.Clamp(extra, 1f, 5f);
    }

    private void OnVisionDarkening(Entity<FishTraumaStatusComponent> ent, ref GetVisionDarkeningEvent args)
    {
        if (ent.Comp.ConcussionSeverity > 0f && _timing.CurTime < ent.Comp.ConcussionUntil)
            args.Strength += 2f + ent.Comp.ConcussionSeverity * 4f;

        if (ent.Comp.ShockFactor >= 0.4f)
            args.Strength += ent.Comp.ShockFactor * 2.5f;
    }

    /// <summary>
    /// ATLS-подобная шкала по доле крови: &lt;15%≈0, 15–30%≈0.25, 30–40%≈0.55, &gt;40%≈0.85+.
    /// </summary>
    public float CalculateShockFactor(EntityUid uid)
    {
        if (!_bloodQuery.TryComp(uid, out var blood))
            return 0f;

        var level = _bloodstream.GetBloodLevel((uid, blood));
        // GetBloodLevel обычно 0–1 доля заполнения.
        var lost = 1f - Math.Clamp(level, 0f, 1f);

        var shock = lost switch
        {
            < 0.15f => lost / 0.15f * 0.1f,
            < 0.30f => 0.1f + (lost - 0.15f) / 0.15f * 0.25f,
            < 0.40f => 0.35f + (lost - 0.30f) / 0.10f * 0.25f,
            _ => 0.6f + Math.Min((lost - 0.40f) / 0.30f, 1f) * 0.4f
        };

        // Активное кровотечение усиливает шок.
        if (blood.BleedAmount > 0f)
            shock = Math.Min(1f, shock + Math.Clamp(blood.BleedAmount / blood.MaxBleedAmount, 0f, 0.25f));

        return Math.Clamp(shock, 0f, 1f);
    }

    public float GetMovementMultiplier(Entity<FishTraumaStatusComponent> ent)
    {
        var mult = 1f;
        mult *= 1f - ent.Comp.LegImpairment * 0.55f;
        mult *= 1f - ent.Comp.ShockFactor * 0.45f;

        if (_timing.CurTime < ent.Comp.DisorientUntil)
            mult *= 0.75f;

        if (ent.Comp.ConcussionSeverity > 0.4f && _timing.CurTime < ent.Comp.ConcussionUntil)
            mult *= 0.7f;

        // Soft-crit: доп. замедление поверх CritCrawlSpeedMultiplier (глубина + шок).
        if (_mobState.IsCritical(ent) && HasComp<FishComplexCritComponent>(ent))
        {
            var health = _crit.GetCritScaleHealth(ent);
            // health 0 → лёгкий крит, -80 → глубокий.
            var depth = Math.Clamp((-health) / 80f, 0f, 1f);
            mult *= MathHelper.Lerp(0.7f, 0.25f, depth);
            mult *= 1f - ent.Comp.ShockFactor * 0.35f;
        }

        return Math.Clamp(mult, 0.08f, 1f);
    }

    /// <summary>Множитель ползания для Crit (уже включает базовый CritCrawl).</summary>
    public float GetCritCrawlMultiplier(EntityUid uid)
    {
        var baseMult = SharedFishComplexCritSystem.CritCrawlSpeedMultiplier;
        if (!TryComp<FishTraumaStatusComponent>(uid, out var trauma))
            return baseMult;

        return baseMult * GetMovementMultiplier((uid, trauma));
    }

    public bool CanAttemptStand(Entity<FishTraumaStatusComponent> ent, out string? reasonKey)
    {
        reasonKey = null;

        if (_mobState.IsDead(ent))
        {
            reasonKey = "fish-trauma-cant-stand-dead";
            return false;
        }

        if (HasComp<FishCritUnconsciousComponent>(ent))
        {
            reasonKey = "fish-crit-cant-stand-unconscious";
            return false;
        }

        // Soft-crit: не блокируем попытку жёстко — только замедляем через GetStandUpTime.
        // Иначе после взрыва/шока кнопка «встать» вообще не стартует do-after.
        if (_mobState.IsCritical(ent) && HasComp<FishComplexCritComponent>(ent))
            return true;

        if (ent.Comp.ShockFactor >= ShockStandBlock)
        {
            reasonKey = "fish-trauma-cant-stand-shock";
            return false;
        }

        if (ent.Comp.LegImpairment >= LegStandBlock)
        {
            reasonKey = "fish-trauma-cant-stand-legs";
            return false;
        }

        if (ent.Comp.ChestTraumaSeverity >= 0.65f && _timing.CurTime < ent.Comp.ChestTraumaUntil)
        {
            reasonKey = "fish-trauma-cant-stand-chest";
            return false;
        }

        if (ent.Comp.ConcussionSeverity >= 0.75f && _timing.CurTime < ent.Comp.ConcussionUntil)
        {
            reasonKey = "fish-trauma-cant-stand-concussion";
            return false;
        }

        return true;
    }

    public void ClearTrauma(Entity<FishTraumaStatusComponent> ent)
    {
        ent.Comp.ShockFactor = 0f;
        ent.Comp.LegImpairment = 0f;
        ent.Comp.ConcussionSeverity = 0f;
        ent.Comp.ConcussionUntil = TimeSpan.Zero;
        ent.Comp.TinnitusUntil = TimeSpan.Zero;
        ent.Comp.DisorientUntil = TimeSpan.Zero;
        ent.Comp.ChestTraumaUntil = TimeSpan.Zero;
        ent.Comp.ChestTraumaSeverity = 0f;
        ent.Comp.LastBlastForce = 0f;
        Dirty(ent);
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
        _darkenedVision.UpdateVisionDarkening(ent.Owner);
    }

    public void EnsureTrauma(EntityUid uid, out FishTraumaStatusComponent trauma)
    {
        trauma = EnsureComp<FishTraumaStatusComponent>(uid);
    }

    /// <summary>Контузия / mTBI (primary blast + blunt head).</summary>
    public void ApplyConcussion(EntityUid uid, float severity, TimeSpan duration)
    {
        EnsureTrauma(uid, out var trauma);
        severity = Math.Clamp(severity, 0.1f, 1f);
        trauma.ConcussionSeverity = Math.Max(trauma.ConcussionSeverity, severity);
        trauma.ConcussionUntil = MaxTime(trauma.ConcussionUntil, _timing.CurTime + duration);
        Dirty(uid, trauma);
        _darkenedVision.UpdateVisionDarkening(uid);
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    public void ApplyTinnitus(EntityUid uid, TimeSpan duration)
    {
        EnsureTrauma(uid, out var trauma);
        trauma.TinnitusUntil = MaxTime(trauma.TinnitusUntil, _timing.CurTime + duration);
        Dirty(uid, trauma);

        if (_timing.IsFirstTimePredicted)
            _popup.PopupEntity(Loc.GetString("fish-trauma-tinnitus"), uid, uid, PopupType.SmallCaution);
    }

    public void ApplyDisorient(EntityUid uid, TimeSpan duration)
    {
        EnsureTrauma(uid, out var trauma);
        trauma.DisorientUntil = MaxTime(trauma.DisorientUntil, _timing.CurTime + duration);
        Dirty(uid, trauma);
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    public void ApplyChestTrauma(EntityUid uid, float severity, TimeSpan duration)
    {
        EnsureTrauma(uid, out var trauma);
        severity = Math.Clamp(severity, 0.1f, 1f);
        trauma.ChestTraumaSeverity = Math.Max(trauma.ChestTraumaSeverity, severity);
        trauma.ChestTraumaUntil = MaxTime(trauma.ChestTraumaUntil, _timing.CurTime + duration);
        Dirty(uid, trauma);
        _movement.RefreshMovementSpeedModifiers(uid);

        if (severity >= 0.4f && _timing.IsFirstTimePredicted)
            _popup.PopupEntity(Loc.GetString("fish-trauma-chest-pain"), uid, uid, PopupType.MediumCaution);
    }

    public void ApplyLegImpairment(EntityUid uid, float amount)
    {
        EnsureTrauma(uid, out var trauma);
        trauma.LegImpairment = Math.Clamp(trauma.LegImpairment + amount, 0f, 1f);
        Dirty(uid, trauma);
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    public void NotifyBlastForce(EntityUid uid, float force)
    {
        EnsureTrauma(uid, out var trauma);
        trauma.LastBlastForce = force;
        Dirty(uid, trauma);
    }

    private static TimeSpan MaxTime(TimeSpan current, TimeSpan candidate)
    {
        return current > candidate ? current : candidate;
    }
}
