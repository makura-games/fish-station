using Content.Server.Popups;
using Content.Shared._Fish.Crit;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using MobStateEnum = Content.Shared.Mobs.MobState;

namespace Content.Server._Fish.Crit;

/// <summary>
/// Серверная логика расширенного Crit: осложнения, CPR, дефиб.
/// </summary>
public sealed partial class FishComplexCritSystem : SharedFishComplexCritSystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StutteringSystem _stutter = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SatiationSystem _satiation = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier FallSound =
        new SoundCollectionSpecifier("FishCritFall", AudioParams.Default.WithVolume(-2f));

    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Slash = "Slash";
    private static readonly ProtoId<DamageTypePrototype> Piercing = "Piercing";
    private static readonly ProtoId<DamageTypePrototype> Heat = "Heat";
    private static readonly ProtoId<DamageTypePrototype> Shock = "Shock";
    private static readonly ProtoId<DamageTypePrototype> Cellular = "Cellular";

    private static readonly ProtoId<ReagentPrototype> Saline = "Saline";
    private static readonly ProtoId<ReagentPrototype> Epinephrine = "Epinephrine";
    private static readonly ProtoId<ReagentPrototype> Heparin = "Heparin";
    private static readonly ProtoId<ReagentPrototype> Atropine = "FishAtropine";


    private static readonly string[] MildFeelKeys =
    [
        "fish-crit-feel-terrible",
        "fish-crit-feel-awful",
        "fish-crit-feel-shit",
        "fish-crit-feel-sick",
        "fish-crit-feel-numb",
        "fish-crit-feel-cold",
        "fish-crit-feel-sweaty",
        "fish-crit-feel-tingly",
        "fish-crit-feel-horrible",
    ];

    private static readonly string[] SevereFeelKeys =
    [
        "fish-crit-feel-horrible-pain",
        "fish-crit-feel-awful",
        "fish-crit-feel-shit",
        "fish-crit-feel-absolutely-awful",
        "fish-crit-feel-like-death",
        "fish-crit-feel-dying",
        "fish-crit-feel-nothing",
        "fish-crit-feel-warm",
        "fish-crit-feel-sweaty",
        "fish-crit-feel-tingly",
        "fish-crit-feel-really-bad",
        "fish-crit-feel-horrible",
    ];

    public override void Initialize()
    {
        base.Initialize();
        InitializeCpr();
        InitializeDefib();
    }

    protected override void PlayCritFallSound(Entity<FishComplexCritComponent> ent)
    {
        _audio.PlayPvs(FallSound, ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var cur = Timing.CurTime;
        var query = EntityQueryEnumerator<FishComplexCritComponent, DamageableComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var crit, out var damageable, out var mobState))
        {
            if (mobState.CurrentState == MobStateEnum.Dead)
                continue;

            if (cur < crit.NextTick)
                continue;

            crit.NextTick = cur + crit.TickInterval;

            // Снимаем маркер полной бессознательности, если паралич уже прошёл.
            // Fire soft-crit suppression uses networked FlammableComponent.OnFire directly.
            if (HasComp<FishCritUnconsciousComponent>(uid) && !_stun.IsParalyzed(uid))
                RemCompDeferred<FishCritUnconsciousComponent>(uid);

            HandleCriticalCondition(uid, crit, damageable, mobState);
            HandleComplications(uid, crit, damageable);
            HandleCardiacArrestTick(uid, damageable);

            if (mobState.CurrentState == MobStateEnum.Critical)
                RefreshCritVision(uid);
        }

        // LoseBreath decay каждый кадр — Remaining в секундах wall-clock.
        var loseQuery = EntityQueryEnumerator<FishLoseBreathComponent>();
        while (loseQuery.MoveNext(out var uid, out var lose))
        {
            if (lose.Remaining <= TimeSpan.Zero)
            {
                RemCompDeferred<FishLoseBreathComponent>(uid);
                continue;
            }

            lose.Remaining -= TimeSpan.FromSeconds(frameTime);
            if (lose.Remaining < TimeSpan.Zero)
                lose.Remaining = TimeSpan.Zero;

            // Раз в ~2 с гасп / кислородный урон; Dirty только здесь (Remaining не networked).
            if (cur >= lose.NextOxyTick)
            {
                lose.NextOxyTick = cur + TimeSpan.FromSeconds(2);
                // Fish-edit: LoseBreath — доп. asphyx. NextOxyTick не сетевой, Dirty не нужен.
                ApplyOxy(uid, 1f);
            }

            if (lose.Remaining <= TimeSpan.Zero)
                RemCompDeferred<FishLoseBreathComponent>(uid);
        }
    }

    /// <summary>
    /// Тик критического состояния: окси-урон, шок/СН/остановка сердца.
    /// </summary>
    private void HandleCriticalCondition(
        EntityUid uid,
        FishComplexCritComponent crit,
        DamageableComponent damageable,
        MobStateComponent mobState)
    {
        var critScaleHealth = GetCritScaleHealth(uid, crit, damageable);
        var damageEnt = (uid, damageable);
        var oxy = GetDamage(damageEnt, Asphyxiation);
        var brute = GetDamage(damageEnt, Blunt) + GetDamage(damageEnt, Slash) + GetDamage(damageEnt, Piercing);
        var burn = GetDamage(damageEnt, Heat) + GetDamage(damageEnt, Shock);
        var brain = GetDamage(damageEnt, Cellular);

        // guaranteed_death_threshold = health + oxy*0.5 - fire*0.67 - brute*0.67
        var guaranteedDeath = critScaleHealth + oxy * 0.5f - burn * 0.67f - brute * 0.67f;

        if (brain >= 120f || guaranteedDeath <= -500f)
        {
            MobStateSys.ChangeMobState(uid, MobStateEnum.Dead, mobState);
            return;
        }

        if (brain >= 100f)
        {
            AdjustLoseBreath(uid, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(50));
            ApplyWeaken(uid, TimeSpan.FromSeconds(60));
        }

        // Dead остаётся на ~200 (иначе Gib@Blunt400). Глубокий Crit — внутри Critical (health <= -80).

        if (critScaleHealth > 0f)
            return;

        if (critScaleHealth <= 0f)
        {
            _stutter.DoStutter(uid, TimeSpan.FromSeconds(10), refresh: true);

            if (_random.Prob(0.07f))
                ApplyConfused(uid, TimeSpan.FromSeconds(4));

            if (_random.Prob(0.05f))
                ApplyParalyse(uid, TimeSpan.FromSeconds(4));
        }

        switch (critScaleHealth)
        {
            case <= -80f:
                ApplyOxy(uid, 1f);
                if (_random.Prob(Math.Clamp(-critScaleHealth * 0.001f, 0f, 0.15f)))
                    TrySetHeartAttack(uid, true);
                if (_random.Prob(Math.Clamp(-critScaleHealth * 0.002f, 0f, 0.25f)))
                    TryContractHeartFailure(uid);
                if (_random.Prob(0.04f))
                {
                    _popup.PopupEntity(Loc.GetString("fish-crit-chest-pain"), uid, uid, PopupType.LargeCaution);
                    ApplyParalyse(uid, TimeSpan.FromSeconds(4));
                    TryContractHeartFailure(uid);
                }
                if (_random.Prob(0.08f))
                    ApplyParalyse(uid, TimeSpan.FromSeconds(10));
                break;

            case <= -50f:
                ApplyOxy(uid, 1f);
                if (_random.Prob(0.10f))
                    TryContractShock(uid);
                if (_random.Prob(Math.Clamp(-critScaleHealth * 0.0008f, 0f, 1f)))
                    TryContractHeartFailure(uid);
                if (_random.Prob(0.06f))
                {
                    _popup.PopupEntity(Loc.GetString(_random.Pick(SevereFeelKeys)), uid, uid, PopupType.MediumCaution);
                    // autoStand: false — soft-crit crawl, без рывка «встать» раз в минуту.
                    ApplyWeaken(uid, TimeSpan.FromSeconds(6));
                }
                if (_random.Prob(0.03f))
                    ApplyParalyse(uid, TimeSpan.FromSeconds(4));
                break;

            default: // -49 .. 0 — уже лежим в Crit, лишний knockdown не нужен
                ApplyOxy(uid, 1f);
                if (_random.Prob(0.03f))
                    TryContractShock(uid);
                if (_random.Prob(0.05f))
                    _popup.PopupEntity(Loc.GetString(_random.Pick(MildFeelKeys)), uid, uid, PopupType.MediumCaution);
                break;
        }
    }

    private void HandleComplications(EntityUid uid, FishComplexCritComponent crit, DamageableComponent damageable)
    {
        if (TryComp<FishCritShockComponent>(uid, out var shock))
            TickShock(uid, crit, damageable, shock);

        if (TryComp<FishHeartFailureComponent>(uid, out var hf))
            TickHeartFailure(uid, hf);
    }

    /// <summary>Тик шока (3 стадии).</summary>
    private void TickShock(EntityUid uid, FishComplexCritComponent crit, DamageableComponent damageable, FishCritShockComponent shock)
    {
        var critScaleHealth = GetCritScaleHealth(uid, crit, damageable);
        var hungerOk = IsHungerAboveHypoglycemia(uid);

        // health >= 25 && nutrition >= HYPOGLYCEMIA → cure
        if (critScaleHealth >= 25f && hungerOk)
        {
            _popup.PopupEntity(Loc.GetString("fish-crit-feel-better"), uid, uid, PopupType.Small);
            RemCompDeferred<FishCritShockComponent>(uid);
            return;
        }

        if (_random.Prob(shock.StageProb))
            shock.Stage = Math.Min(shock.Stage + 1, shock.MaxStages);

        if (HasCureReagent(uid, Saline) && _random.Prob(shock.CureProb))
        {
            RemCompDeferred<FishCritShockComponent>(uid);
            return;
        }

        // Случайное самоизлечение 1% * 10%
        if (_random.Prob(0.01f) && _random.Prob(0.10f))
        {
            _popup.PopupEntity(Loc.GetString("fish-crit-feel-better"), uid, uid, PopupType.Small);
            RemCompDeferred<FishCritShockComponent>(uid);
            return;
        }

        switch (shock.Stage)
        {
            case 1:
                if (_random.Prob(0.05f))
                    _popup.PopupEntity(Loc.GetString("fish-crit-shock-weak"), uid, uid, PopupType.SmallCaution);
                break;
            case 2:
                if (_random.Prob(0.05f))
                    _popup.PopupEntity(Loc.GetString("fish-crit-shock-terrible"), uid, uid, PopupType.MediumCaution);
                break;
            default:
                if (_random.Prob(0.05f))
                    _popup.PopupEntity(Loc.GetString("fish-crit-shock-horrible"), uid, uid, PopupType.MediumCaution);
                if (_random.Prob(0.07f))
                {
                    _popup.PopupEntity(Loc.GetString("fish-crit-cant-breathe"), uid, uid, PopupType.LargeCaution);
                    AdjustLoseBreath(uid, TimeSpan.FromSeconds(2));
                }
                if (_random.Prob(0.05f))
                    TryContractHeartFailure(uid);
                break;
        }

        Dirty(uid, shock);
    }

    /// <summary>Тик сердечной недостаточности (3 стадии).</summary>
    private void TickHeartFailure(EntityUid uid, FishHeartFailureComponent hf)
    {
        if (_random.Prob(hf.StageProb))
            hf.Stage = Math.Min(hf.Stage + 1, hf.MaxStages);

        if (HasAnyCureReagent(uid, Epinephrine, Heparin, Atropine) && _random.Prob(hf.CureProb))
        {
            RemCompDeferred<FishHeartFailureComponent>(uid);
            return;
        }

        if (_random.Prob(0.01f) && _random.Prob(0.10f))
        {
            _popup.PopupEntity(Loc.GetString("fish-crit-feel-better"), uid, uid, PopupType.Small);
            RemCompDeferred<FishHeartFailureComponent>(uid);
            return;
        }

        switch (hf.Stage)
        {
            case 1:
                if (_random.Prob(0.05f))
                    _popup.PopupEntity(Loc.GetString("fish-crit-arm-hurts"), uid, uid, PopupType.SmallCaution);
                else if (_random.Prob(0.05f))
                    _popup.PopupEntity(Loc.GetString("fish-crit-chest-hurts"), uid, uid, PopupType.SmallCaution);
                break;
            case 2:
                if (_random.Prob(0.05f))
                {
                    _popup.PopupEntity(Loc.GetString("fish-crit-heart-lurches"), uid, uid, PopupType.MediumCaution);
                    AdjustLoseBreath(uid, TimeSpan.FromSeconds(2));
                }
                if (_random.Prob(0.03f))
                {
                    _popup.PopupEntity(Loc.GetString("fish-crit-heart-stops"), uid, uid, PopupType.LargeCaution);
                    AdjustLoseBreath(uid, TimeSpan.FromSeconds(6));
                }
                break;
            default:
                ApplyOxy(uid, 1f);
                if (_random.Prob(0.05f))
                    TrySetHeartAttack(uid, true);
                break;
        }

        Dirty(uid, hf);
    }

    /// <summary>Тик остановки сердца.</summary>
    private void HandleCardiacArrestTick(EntityUid uid, DamageableComponent damageable)
    {
        if (!IsUndergoingCardiacArrest(uid))
            return;

        // Corazone / аналоги не портированы — только наличие сердца через компонент.
        var oxy = GetDamage((uid, damageable), Asphyxiation);
        if (oxy > 0f)
            ApplyCellular(uid, 3f);
        else if (_random.Prob(0.10f))
            ApplyCellular(uid, 1f);

        ApplyWeaken(uid, TimeSpan.FromSeconds(10));
        AdjustLoseBreath(uid, TimeSpan.FromSeconds(40), TimeSpan.FromSeconds(50));
        ApplyOxy(uid, 20f);
    }

    private void TryContractShock(EntityUid uid)
    {
        if (HasComp<FishCritShockComponent>(uid))
            return;

        EnsureComp<FishCritShockComponent>(uid);
    }

    private void TryContractHeartFailure(EntityUid uid)
    {
        if (HasComp<FishHeartFailureComponent>(uid))
            return;

        EnsureComp<FishHeartFailureComponent>(uid);
    }

    private void ApplyOxy(EntityUid uid, float amount)
    {
        // interruptsDoAfters: false — не рвём do-after вставания; урон oxy (в т.ч. от остановки сердца) всё равно идёт.
        var spec = new DamageSpecifier(_proto.Index(Asphyxiation), FixedPoint2.New(amount));
        Damageable.TryChangeDamage(uid, spec, ignoreResistances: true, interruptsDoAfters: false);
    }

    private void ApplyCellular(EntityUid uid, float amount)
    {
        var spec = new DamageSpecifier(_proto.Index(Cellular), FixedPoint2.New(amount));
        Damageable.TryChangeDamage(uid, spec, ignoreResistances: true, interruptsDoAfters: false);
    }

    private void ApplyParalyse(EntityUid uid, TimeSpan duration)
    {
        EnsureComp<FishCritUnconsciousComponent>(uid);
        _stun.TryUpdateParalyzeDuration(uid, duration);
        // Снимаем маркер когда паралич кончится — через отложенную проверку на следующем тике Stun.
        // Упрощение: маркер живёт пока есть Stun; очищаем в Update если не парализован.
    }

    private void ApplyWeaken(EntityUid uid, TimeSpan duration)
    {
        // Не сбрасываем прогресс «встать» повторным knockdown.
        if (IsTryingToStand(EntityManager, uid))
            return;

        // Soft-crit: не валим снова того, кто уже встал, и не крутим NextUpdate —
        // иначе KnockdownOver всегда false и кнопка «встать» мёртвая.
        if (MobStateSys.IsCritical(uid) && HasComp<FishComplexCritComponent>(uid))
        {
            if (!TryComp<KnockedDownComponent>(uid, out var knocked))
                return;

            _stun.SetKnockdownTime((uid, knocked), TimeSpan.Zero);
            _stun.SetAutoStand((uid, knocked), false);
            return;
        }

        _stun.TryKnockdown(uid, duration, refresh: true, autoStand: false, drop: true);
    }

    private void ApplyKnockdown(EntityUid uid, TimeSpan duration)
    {
        if (IsTryingToStand(EntityManager, uid))
            return;

        if (MobStateSys.IsCritical(uid) && HasComp<FishComplexCritComponent>(uid))
        {
            if (!TryComp<KnockedDownComponent>(uid, out var knocked))
                return;

            _stun.SetKnockdownTime((uid, knocked), TimeSpan.Zero);
            _stun.SetAutoStand((uid, knocked), false);
            return;
        }

        _stun.TryKnockdown(uid, duration, refresh: true, autoStand: false, drop: false);
    }

    private void ApplyConfused(EntityUid uid, TimeSpan duration)
    {
        // Отдельного Confused нет — stutter.
        _stutter.DoStutter(uid, duration, refresh: false);
    }

    private float GetDamage(Entity<DamageableComponent> ent, ProtoId<DamageTypePrototype> type)
    {
        // DamageableComponent.Damage закрыт (RA0002); читаем один тип без клона DamageSpecifier.
        return Damageable.TryGetDamage(ent, type, out var value) ? (float)value : 0f;
    }

    private bool IsHungerAboveHypoglycemia(EntityUid uid)
    {
        // NUTRITION_LEVEL_HYPOGLYCEMIA=100 при max~600 → 33 на шкале Fish (max 200).
        if (!TryComp<SatiationComponent>(uid, out var sat))
            return true;

        var value = _satiation.GetValueOrNull((uid, sat), SatiationSystem.Hunger);
        return value is null or >= 33f;
    }

    private bool HasCureReagent(EntityUid uid, ProtoId<ReagentPrototype> reagent)
    {
        return HasAnyCureReagent(uid, reagent);
    }

    private bool HasAnyCureReagent(EntityUid uid, params ProtoId<ReagentPrototype>[] reagents)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var blood))
            return false;

        foreach (var name in new[] { blood.BloodSolutionName, blood.MetabolitesSolutionName })
        {
            if (!_solution.TryGetSolution(uid, name, out _, out var solution))
                continue;

            foreach (var reagent in reagents)
            {
                if (solution.ContainsReagent(new ReagentId(reagent, null)))
                    return true;
            }
        }

        return false;
    }
}
