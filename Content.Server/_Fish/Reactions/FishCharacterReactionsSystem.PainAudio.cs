using Content.Shared._Fish.Crit;
using Content.Shared._Fish.Reactions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Speech.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Fish.Reactions;

public sealed partial class FishCharacterReactionsSystem
{
    private static readonly ProtoId<SoundCollectionPrototype> SharpMale = "FishPainSharpMale";
    private static readonly ProtoId<SoundCollectionPrototype> SharpFemale = "FishPainSharpFemale";
    private static readonly ProtoId<SoundCollectionPrototype> MoanMale = "FishPainMoanMale";
    private static readonly ProtoId<SoundCollectionPrototype> MoanFemale = "FishPainMoanFemale";
    private static readonly ProtoId<SoundCollectionPrototype> LongMale = "FishPainLongMale";
    private static readonly ProtoId<SoundCollectionPrototype> BurnMale = "FishPainBurnMale";
    private static readonly ProtoId<SoundCollectionPrototype> FallMale = "FishPainFallMale";
    private static readonly ProtoId<SoundCollectionPrototype> PanicMale = "FishPainPanicMale";
    private static readonly ProtoId<SoundCollectionPrototype> GaspCol = "FishPainGasp";
    private static readonly ProtoId<SoundCollectionPrototype> StandMale = "FishPainStandMale";
    private static readonly ProtoId<SoundCollectionPrototype> DeathMale = "FishPainDeathMale";
    private static readonly ProtoId<SoundCollectionPrototype> CoughMale = "FishPainCoughMale";
    private static readonly ProtoId<SoundCollectionPrototype> CoughFemale = "FishPainCoughFemale";

    private void InitializePainAudio()
    {
        SubscribeLocalEvent<FishCharacterReactionsComponent, StandUpAttemptEvent>(OnStandPainSound);
    }

    private void OnStandPainSound(Entity<FishCharacterReactionsComponent> ent, ref StandUpAttemptEvent args)
    {
        if (args.Cancelled || args.Autostand || MobStateSys.IsDead(ent) || HasComp<FishCritUnconsciousComponent>(ent))
            return;

        if (Timing.CurTime < ent.Comp.NextPainSound)
            return;

        if (!_random.Prob(0.45f))
            return;

        PlayPainSound(ent, FishPainSoundKind.Stand);
        ent.Comp.NextPainSound = Timing.CurTime + TimeSpan.FromSeconds(2.5);
        Dirty(ent);
    }

    private void TryPainSound(Entity<FishCharacterReactionsComponent> ent, DamageChangedEvent args)
    {
        if (Timing.CurTime < ent.Comp.NextPainSound)
            return;

        if (args.DamageDelta == null)
            return;

        var pain = GetPainDamage(args.DamageDelta);
        if (pain < ent.Comp.PainMinDamage)
            return;

        if (!_random.Prob(0.55f))
            return;

        var kind = ClassifyPainSound(args.DamageDelta, pain);
        PlayPainSound(ent, kind);

        var heat = args.DamageDelta.DamageDict.TryGetValue(HeatDamage, out var h) && h > 0;
        if (heat)
            _audio.PlayPvs(ent.Comp.BurnSound, ent);

        _jitter.DoJitter(
            ent,
            ent.Comp.PainJitterDuration,
            refresh: true,
            amplitude: heat ? ent.Comp.PainJitterAmplitude * 1.4f : ent.Comp.PainJitterAmplitude,
            frequency: heat ? 6f : 4f);

        ent.Comp.NextPainSound = Timing.CurTime + ent.Comp.PainSoundCooldown;
        Dirty(ent);
    }

    private static FishPainSoundKind ClassifyPainSound(DamageSpecifier delta, float pain)
    {
        var heat = GetDmg(delta, "Heat");
        var blunt = GetDmg(delta, "Blunt");
        var shock = GetDmg(delta, "Shock");
        var slash = GetDmg(delta, "Slash");
        var pierce = GetDmg(delta, "Piercing");

        if (heat >= 8f || heat > 0f && heat >= blunt * 0.8f)
            return FishPainSoundKind.Burn;

        if (shock >= 10f)
            return FishPainSoundKind.Sharp;

        if (pain >= 28f || blunt >= 25f)
            return FishPainSoundKind.Long;

        if (blunt >= 14f && slash + pierce < blunt * 0.5f)
            return FishPainSoundKind.Fall;

        if (pain <= 12f && (slash > 0 || pierce > 0))
            return FishPainSoundKind.Moan;

        return FishPainSoundKind.Sharp;
    }

    /// <summary>
    /// Человеческий вокал из Fish CC0-библиотеки.
    /// Vocal/MaleScreams — fallback только для сильных категорий.
    /// </summary>
    private void PlayPainSound(Entity<FishCharacterReactionsComponent> ent, FishPainSoundKind kind = FishPainSoundKind.Sharp)
    {
        if (MobStateSys.IsDead(ent) || HasComp<FishCritUnconsciousComponent>(ent))
        {
            if (kind is not (FishPainSoundKind.Gasp or FishPainSoundKind.Death or FishPainSoundKind.Cough))
                return;
        }

        var female = TryComp<HumanoidProfileComponent>(ent, out var profile) && profile.Sex == Sex.Female;
        var primary = ResolvePainCollection(kind, female);
        var secondary = ResolvePainCollection(kind, !female);

        var volume = kind switch
        {
            FishPainSoundKind.Panic => 5f,
            FishPainSoundKind.Burn => 5f,
            FishPainSoundKind.Long => 4f,
            FishPainSoundKind.Death => 2f,
            FishPainSoundKind.Gasp => 1f,
            FishPainSoundKind.Stand => 0f,
            FishPainSoundKind.Moan => 1f,
            FishPainSoundKind.Cough => 0f,
            _ => 3f
        };

        var parms = AudioParams.Default.WithVolume(volume).WithVariation(0.07f);

        if (TryPlayCollection(ent.Owner, primary, parms))
            return;

        if (primary != secondary && TryPlayCollection(ent.Owner, secondary, parms))
            return;

        // Женский long/burn/panic/fall без отдельного пакета — sharp/moan female.
        if (female && kind is FishPainSoundKind.Long or FishPainSoundKind.Burn or FishPainSoundKind.Panic or FishPainSoundKind.Fall)
        {
            if (TryPlayCollection(ent.Owner, SharpFemale, parms))
                return;
            if (TryPlayCollection(ent.Owner, MoanFemale, parms))
                return;
        }

        if (kind is FishPainSoundKind.Sharp or FishPainSoundKind.Long or FishPainSoundKind.Burn or FishPainSoundKind.Panic)
        {
            if (TryComp<VocalComponent>(ent, out var vocal) &&
                vocal.EmoteSounds is { } soundsId &&
                _proto.TryIndex(soundsId, out var sounds) &&
                _chat.TryPlayEmoteSound(ent.Owner, sounds, vocal.ScreamId, parms))
            {
                return;
            }

            var fallback = female ? ent.Comp.FemalePainSound : ent.Comp.MalePainSound;
            _audio.PlayPvs(fallback, ent, parms);
        }
    }

    private bool TryPlayCollection(EntityUid uid, ProtoId<SoundCollectionPrototype> id, AudioParams parms)
    {
        if (!_proto.TryIndex(id, out SoundCollectionPrototype? col) || col.PickFiles.Count == 0)
            return false;

        _audio.PlayPvs(new SoundCollectionSpecifier(id, parms), uid);
        return true;
    }

    private static ProtoId<SoundCollectionPrototype> ResolvePainCollection(FishPainSoundKind kind, bool female)
    {
        return kind switch
        {
            // Женских long/burn/panic/fall пакетов нет — используем sharp/moan female, не male.
            FishPainSoundKind.Sharp => female ? SharpFemale : SharpMale,
            FishPainSoundKind.Moan => female ? MoanFemale : MoanMale,
            FishPainSoundKind.Long => female ? SharpFemale : LongMale,
            FishPainSoundKind.Burn => female ? MoanFemale : BurnMale,
            FishPainSoundKind.Fall => female ? MoanFemale : FallMale,
            FishPainSoundKind.Panic => female ? SharpFemale : PanicMale,
            FishPainSoundKind.Gasp => GaspCol,
            FishPainSoundKind.Stand => female ? MoanFemale : StandMale,
            FishPainSoundKind.Death => DeathMale,
            FishPainSoundKind.Cough => female ? CoughFemale : CoughMale,
            _ => female ? SharpFemale : SharpMale
        };
    }
}
