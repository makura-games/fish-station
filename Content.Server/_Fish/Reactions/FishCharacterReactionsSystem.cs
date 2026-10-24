using Content.Shared._Fish.Crit;
using Content.Shared._Fish.Reactions;
using Content.Shared._Sunrise.Footprints;
using Content.Shared.Atmos;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Jittering;
using Content.Server.Chat.Systems;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Speech.Components;
using Content.Shared.Standing;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Fish.Reactions;

/// <summary>
/// Серверные реакции: болевой крик (звук), кровь на полу/следы, детонация взрывчатки в руках.
/// </summary>
public sealed partial class FishCharacterReactionsSystem : SharedFishCharacterReactionsSystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private TriggerSystem _trigger = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedFishComplexCritSystem _crit = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<DamageTypePrototype> HeatDamage = "Heat";
    private static readonly ProtoId<DamageTypePrototype> SlashDamage = "Slash";
    private static readonly ProtoId<DamageTypePrototype> PiercingDamage = "Piercing";
    private static readonly ProtoId<DamageTypePrototype> BluntDamage = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> AsphyxiationDamage = "Asphyxiation";
    private static readonly ProtoId<DamageTypePrototype> PoisonDamage = "Poison";
    private static readonly ProtoId<DamageTypePrototype> BloodlossDamage = "Bloodloss";
    private static readonly ProtoId<DamageTypePrototype> CellularDamage = "Cellular";
    private static readonly ProtoId<DamageTypePrototype> ColdDamage = "Cold";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishCharacterReactionsComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<FishCharacterReactionsComponent, IgnitedEvent>(OnIgnited);
        SubscribeLocalEvent<FishCharacterReactionsComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<FishCharacterReactionsComponent, FishGotHitByProjectileEvent>(OnGotHitByProjectile);
        InitializeBlast();
        InitializePainAudio();
    }

    private void OnDamageChanged(Entity<FishCharacterReactionsComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        if (MobStateSys.IsDead(ent))
            return;

        // Кровь на полу/теле даже в unconscious; звук/травма — только если в сознании.
        TryBloodOnHit(ent, args);

        if (HasComp<FishCritUnconsciousComponent>(ent))
            return;

        TryPainSound(ent, args);
        TryApplyDamageTrauma(ent, args);
    }

    private void OnGotHitByProjectile(Entity<FishCharacterReactionsComponent> ent, ref FishGotHitByProjectileEvent args)
    {
        if (MobStateSys.IsDead(ent) || HasComp<FishCritUnconsciousComponent>(ent))
            return;

#pragma warning disable CS0618
        var total = args.Damage.GetTotal().Float();
#pragma warning restore CS0618
        if (total < ent.Comp.HeldExplosiveMinDamage)
            return;

        TryDetonateHeldExplosive(ent);
    }

    private void OnIgnited(Entity<FishCharacterReactionsComponent> ent, ref IgnitedEvent args)
    {
        if (MobStateSys.IsDead(ent))
            return;

        // Боль до маркера бессознательности — иначе в Crit на огне крик никогда не сыграет.
        // OnFire is AutoNetworkedField: soft-crit stand suppression reads it on the client.
        if (!HasComp<FishCritUnconsciousComponent>(ent) && Timing.CurTime >= ent.Comp.NextPainSound)
        {
            PlayPainSound(ent, FishPainSoundKind.Burn);
            _audio.PlayPvs(ent.Comp.BurnSound, ent);
            _jitter.DoJitter(ent, TimeSpan.FromSeconds(2), refresh: true, amplitude: 14f, frequency: 6f);
            ent.Comp.NextPainSound = Timing.CurTime + ent.Comp.PainSoundCooldown;
            Dirty(ent);
        }
    }

    private void OnMove(Entity<FishCharacterReactionsComponent> ent, ref MoveEvent args)
    {
        if (args.OnlyRotation || args.ParentChanged)
            return;

        TryBleedTrail(ent);
    }

    /// <summary>
    /// Детонация взрывчатки в руке (~15%) при попадании снаряда.
    /// </summary>
    private void TryDetonateHeldExplosive(Entity<FishCharacterReactionsComponent> ent)
    {
        if (!_random.Prob(ent.Comp.HeldExplosiveDetonateChance))
            return;

        foreach (var held in _hands.EnumerateHeld(ent.Owner))
        {
            if (!HasComp<ExplosiveComponent>(held))
                continue;

            // Гранаты / бомбы: ExplodeOnTrigger или любой Explosive в руках.
            var canBoom = HasComp<ExplodeOnTriggerComponent>(held) || HasComp<ExplosionOnTriggerComponent>(held);
            if (!canBoom)
                continue;

            _popup.PopupEntity(
                Loc.GetString("fish-reactions-grenade-shot", ("item", held)),
                ent,
                PopupType.LargeCaution);

            // key null — срабатывают все OnTrigger-эффекты; "timer" — штатный ключ гранат.
            if (!_trigger.Trigger(held, ent.Owner, key: "timer"))
                _trigger.Trigger(held, ent.Owner, key: null);

            // Не QueueDel — иначе взрыв может не успеть обработаться; ExplodeOnTrigger сам удаляет.
            return;
        }
    }

    /// <summary>
    /// Брызги крови на пол + кровь на ноги/тело для следов (пуля и ближний бой).
    /// </summary>
    private void TryBloodOnHit(Entity<FishCharacterReactionsComponent> ent, DamageChangedEvent args)
    {
        if (args.DamageDelta == null)
            return;

        var bleedDmg = GetBleedDamage(args.DamageDelta);
        if (bleedDmg < ent.Comp.HitBloodMinDamage)
            return;

        if (!TryComp<BloodstreamComponent>(ent, out var blood))
            return;

        var scale = Math.Clamp(bleedDmg / 10f, 0.5f, 2.5f);
        var spill = FixedPoint2.New(ent.Comp.HitBloodSpill * scale);
        _bloodstream.TryBleedOut((ent.Owner, blood), spill);

        if (!TryComp<FootprintEmitterComponent>(ent, out var emitter))
            return;

        var surfaceAmount = FixedPoint2.New(ent.Comp.HitBloodOnSurface * scale);
        // Лёжа — body_surface (как TryBleedTrail), иначе foots.
        var standing = !_standing.IsDown(ent.Owner);
        AddBloodToSurface(ent.Owner, emitter, blood, surfaceAmount, standingPreferFoots: standing);
        _audio.PlayPvs(ent.Comp.BleedSound, ent);
    }

    /// <summary>
    /// Кровоточащий след при движении: foots стоя / body_surface лёжа.
    /// </summary>
    private void TryBleedTrail(Entity<FishCharacterReactionsComponent> ent)
    {
        if (Timing.CurTime < ent.Comp.NextBleedTrail)
            return;

        if (!TryComp<BloodstreamComponent>(ent, out var blood) || blood.BleedAmount <= 0)
            return;

        if (!TryComp<FootprintEmitterComponent>(ent, out var emitter))
            return;

        var amount = FixedPoint2.New(ent.Comp.BleedTrailTransfer * Math.Clamp(blood.BleedAmount / 5f, 0.35f, 2.5f));
        var standing = !_standing.IsDown(ent.Owner);
        if (!AddBloodToSurface(ent.Owner, emitter, blood, amount, standingPreferFoots: standing))
            return;

        // Следы рисует FootprintSystem на MoveEvent — мы только наполняем раствор.
        ent.Comp.NextBleedTrail = Timing.CurTime + ent.Comp.BleedTrailCooldown;
        Dirty(ent);
    }

    private bool AddBloodToSurface(
        EntityUid uid,
        FootprintEmitterComponent emitter,
        BloodstreamComponent blood,
        FixedPoint2 amount,
        bool standingPreferFoots)
    {
        var solutionName = standingPreferFoots ? emitter.FootsSolutionName : emitter.BodySurfaceSolutionName;
        Entity<SolutionComponent>? solEnt = standingPreferFoots ? emitter.FootsSolution : emitter.BodySurfaceSolution;

        if (!_solutions.ResolveSolution(uid, solutionName, ref solEnt, out var solution))
            return false;

        if (standingPreferFoots)
            emitter.FootsSolution = solEnt;
        else
            emitter.BodySurfaceSolution = solEnt;

        var reagent = blood.BloodReferenceSolution.Contents.Count > 0
            ? blood.BloodReferenceSolution.Contents[0].Reagent
            : new ReagentId("Blood", null);

        // TryAddReagent respects MaxVolume and keeps ReagentId data (unlike Solution.AddReagent).
        if (!_solutions.TryAddReagent(solEnt.Value, reagent, amount, out _))
            return false;

        return true;
    }

    /// <summary>Урон, который оставляет кровь (slash/pierce/blunt).</summary>
    private static float GetBleedDamage(DamageSpecifier delta)
    {
        float total = 0f;
        if (delta.DamageDict.TryGetValue(SlashDamage, out var s))
            total += s.Float();
        if (delta.DamageDict.TryGetValue(PiercingDamage, out var p))
            total += p.Float();
        if (delta.DamageDict.TryGetValue(BluntDamage, out var b))
            total += b.Float() * 0.35f;
        return total;
    }

    /// <summary>
    /// Урон для крика: игнорируем asphyx/poison/bloodloss/cellular/cold (тик Crit / воздух / хил-шум).
    /// </summary>
    private static float GetPainDamage(DamageSpecifier delta)
    {
        float total = 0f;
        foreach (var (type, amount) in delta.DamageDict)
        {
            if (amount <= 0)
                continue;
            if (type == AsphyxiationDamage || type == PoisonDamage || type == BloodlossDamage ||
                type == CellularDamage || type == ColdDamage)
                continue;
            total += amount.Float();
        }

        return total;
    }
}
