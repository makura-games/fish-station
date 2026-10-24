using Content.Shared._Fish.Crit;
using Content.Shared._Fish.Reactions;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Drunk;
using Content.Shared.FixedPoint;
using Content.Shared.Jittering;
using Content.Shared.Popups;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Reactions;

/// <summary>
/// Blast injury (Merck/CDC/DoD): primary overpressure + tertiary wall impact.
/// Secondary (осколки) и quaternary (ожоги) идут через обычный Damageable / Flammable.
/// </summary>
public sealed partial class FishCharacterReactionsSystem
{
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StutteringSystem _stutter = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDrunkSystem _drunk = default!;
    [Dependency] private SharedFishTraumaSystem _trauma = default!;
    // _proto / _audio / _popup / _jitter / MobStateSys — из основной partial

    private static readonly ProtoId<DamageTypePrototype> BlastBlunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> BlastAsphyx = "Asphyxiation";

    private void InitializeBlast()
    {
        SubscribeLocalEvent<FishBlastedComponent, ComponentStartup>(OnBlastedStartup);
        SubscribeLocalEvent<FishBlastedComponent, ThrowDoHitEvent>(OnBlastDoHit);
        SubscribeLocalEvent<FishBlastedComponent, StopThrowEvent>(OnBlastStopThrow);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var cur = Timing.CurTime;
        var query = EntityQueryEnumerator<FishBlastedComponent>();
        while (query.MoveNext(out var uid, out var blast))
        {
            if (cur >= blast.Expires)
                RemCompDeferred<FishBlastedComponent>(uid);
        }
    }

    private void OnBlastedStartup(Entity<FishBlastedComponent> ent, ref ComponentStartup args)
    {
        // EnsureComp вызывает Startup до присвоения ThrowSpeed — ждём ApplyBlastEffects.
        if (ent.Comp.ThrowSpeed <= 0f)
            return;

        ApplyBlastEffects(ent);
    }

    /// <summary>
    /// Применить primary blast-эффекты после выставления ThrowSpeed (вызов из ExplosionSystem).
    /// </summary>
    public void ApplyBlastEffects(Entity<FishBlastedComponent> ent)
    {
        if (MobStateSys.IsDead(ent))
            return;

        var speed = Math.Max(ent.Comp.ThrowSpeed, 0.5f);
        var severity = Math.Clamp(speed / 12f, 0.2f, 1f);

        _trauma.NotifyBlastForce(ent.Owner, speed);

        // Primary: оглушение / knockdown (ударная волна).
        _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(Math.Clamp(speed * 0.35f, 2f, 6f)),
            refresh: true, autoStand: false, drop: true);

        // Primary: acoustic barotrauma → tinnitus + дезориентация (drunk как суррогат).
        _trauma.ApplyTinnitus(ent.Owner, TimeSpan.FromSeconds(8 + severity * 20));
        _trauma.ApplyDisorient(ent.Owner, TimeSpan.FromSeconds(4 + severity * 10));
        _drunk.TryApplyDrunkenness(ent.Owner, TimeSpan.FromSeconds(6 + severity * 14));

        // Primary: mild TBI / concussion.
        _trauma.ApplyConcussion(ent.Owner, 0.25f + severity * 0.45f, TimeSpan.FromSeconds(10 + severity * 25));

        // Primary: blast lung — дыхательная травма при сильной волне.
        if (severity >= 0.35f)
        {
            _trauma.ApplyChestTrauma(ent.Owner, severity * 0.7f, TimeSpan.FromSeconds(12 + severity * 20));
            var asphyx = FixedPoint2.New(Math.Clamp(severity * 12f, 3f, 18f));
            _damageable.TryChangeDamage(ent.Owner,
                new DamageSpecifier(_proto.Index(BlastAsphyx), asphyx),
                ignoreResistances: true,
                interruptsDoAfters: false,
                origin: null);
            _crit.AdjustLoseBreath(ent.Owner, TimeSpan.FromSeconds(4 + severity * 10));

            if (TryComp<FishCharacterReactionsComponent>(ent, out var gaspReact))
                PlayPainSound((ent.Owner, gaspReact), FishPainSoundKind.Gasp);
        }

        _stutter.DoStutter(ent.Owner, TimeSpan.FromSeconds(8 + severity * 8), refresh: true);
        _jitter.DoJitter(ent.Owner, TimeSpan.FromSeconds(3), refresh: true, amplitude: 16f, frequency: 7f);

        if (TryComp<FishCharacterReactionsComponent>(ent, out var reactions))
        {
            PlayPainSound((ent.Owner, reactions), FishPainSoundKind.Panic);
            reactions.NextPainSound = Timing.CurTime + reactions.PainSoundCooldown;
            Dirty(ent.Owner, reactions);
        }

        _popup.PopupEntity(Loc.GetString("fish-reactions-blast-wave"), ent.Owner, ent.Owner, PopupType.LargeCaution);
    }

    private void OnBlastDoHit(Entity<FishBlastedComponent> ent, ref ThrowDoHitEvent args)
    {
        if (MobStateSys.IsDead(ent))
            return;

        if (!TryComp<PhysicsComponent>(args.Target, out var otherPhys))
            return;

        // Стена / тяжёлая конструкция — не другой лёгкий моб.
        if (otherPhys.BodyType != BodyType.Static && otherPhys.Mass < 100f)
            return;

        ApplyWallSlam(ent);
    }

    private void OnBlastStopThrow(Entity<FishBlastedComponent> ent, ref StopThrowEvent args)
    {
        // Expires чистит маркер; повторные столкновения возможны до конца броска.
    }

    /// <summary>
    /// Tertiary blast: тело врезается в поверхность → blunt TBI, переломы (leg), оглушение.
    /// </summary>
    private void ApplyWallSlam(Entity<FishBlastedComponent> ent)
    {
        var speed = Math.Max(ent.Comp.ThrowSpeed, 1f);
        var severity = Math.Clamp(speed / 10f, 0.3f, 1f);
        var stunSec = Math.Clamp(speed * 0.45f, 3f, 8f);
        var blunt = FixedPoint2.New(Math.Clamp(speed * 1.8f, 8f, 35f));

        _stun.TryUpdateParalyzeDuration(ent.Owner, TimeSpan.FromSeconds(stunSec));
        _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(stunSec), refresh: true, autoStand: false, drop: true);

        var dmg = new DamageSpecifier(_proto.Index(BlastBlunt), blunt);
        _damageable.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false, interruptsDoAfters: true, origin: null);

        // Удар головой/туловищем о стену.
        _trauma.ApplyConcussion(ent.Owner, 0.5f + severity * 0.5f, TimeSpan.FromSeconds(15 + severity * 30));
        _trauma.ApplyDisorient(ent.Owner, TimeSpan.FromSeconds(8 + severity * 12));
        _trauma.ApplyLegImpairment(ent.Owner, 0.25f + severity * 0.4f);
        _drunk.TryApplyDrunkenness(ent.Owner, TimeSpan.FromSeconds(10 + severity * 15));

        _stutter.DoStutter(ent.Owner, TimeSpan.FromSeconds(12), refresh: true);
        _jitter.DoJitter(ent.Owner, TimeSpan.FromSeconds(2.5), refresh: true, amplitude: 20f, frequency: 8f);

        if (TryComp<FishCharacterReactionsComponent>(ent, out var reactions))
        {
            PlayPainSound((ent.Owner, reactions), FishPainSoundKind.Long);
            reactions.NextPainSound = Timing.CurTime + TimeSpan.FromSeconds(2);
            Dirty(ent.Owner, reactions);
        }

        _popup.PopupEntity(Loc.GetString("fish-reactions-blast-wall"), ent.Owner, ent.Owner, PopupType.LargeCaution);
        _audio.PlayPvs(new SoundCollectionSpecifier("FishCritFall"), ent.Owner);

        RemCompDeferred<FishBlastedComponent>(ent.Owner);
    }
}
