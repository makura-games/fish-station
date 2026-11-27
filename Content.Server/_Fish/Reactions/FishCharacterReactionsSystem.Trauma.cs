using Content.Shared._Fish.Reactions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Drunk;
using Robust.Shared.Random;

namespace Content.Server._Fish.Reactions;

/// <summary>
/// Реакции по типу урона → травма (игровой адапт физиологии, без отдельного симулятора органов).
/// </summary>
public sealed partial class FishCharacterReactionsSystem
{
    private void TryApplyDamageTrauma(Entity<FishCharacterReactionsComponent> ent, DamageChangedEvent args)
    {
        if (args.DamageDelta == null)
            return;

        var delta = args.DamageDelta;
        var blunt = GetDmg(delta, "Blunt");
        var slash = GetDmg(delta, "Slash");
        var pierce = GetDmg(delta, "Piercing");
        var heat = GetDmg(delta, "Heat");
        var shock = GetDmg(delta, "Shock");
        var asphyx = GetDmg(delta, "Asphyxiation");

        // Сильный blunt (удар / tertiary) → контузия / дезориентация.
        if (blunt >= 15f)
        {
            var sev = Math.Clamp(blunt / 40f, 0.2f, 1f);
            _trauma.ApplyConcussion(ent.Owner, sev, TimeSpan.FromSeconds(8 + sev * 20));
            _trauma.ApplyDisorient(ent.Owner, TimeSpan.FromSeconds(3 + sev * 8));
            if (blunt >= 25f)
                _drunk.TryApplyDrunkenness(ent.Owner, TimeSpan.FromSeconds(5 + sev * 10));
        }

        // Проникающие / резаные: хромота (эвристика без BodyPart).
        var limbTrauma = slash * 0.6f + pierce * 0.7f + blunt * 0.25f;
        if (limbTrauma >= 10f)
            _trauma.ApplyLegImpairment(ent.Owner, Math.Clamp(limbTrauma / 50f, 0.1f, 0.55f));

        // Грудь / дыхание.
        if (asphyx >= 8f || blunt + pierce >= 22f)
        {
            var chestSev = Math.Clamp(Math.Max(asphyx / 25f, (blunt + pierce) / 45f), 0.2f, 1f);
            _trauma.ApplyChestTrauma(ent.Owner, chestSev, TimeSpan.FromSeconds(10 + chestSev * 15));
            if (chestSev >= 0.45f)
                _crit.AdjustLoseBreath(ent.Owner, TimeSpan.FromSeconds(3 + chestSev * 8));

            if (Timing.CurTime >= ent.Comp.NextPainSound && _random.Prob(0.4f))
            {
                PlayPainSound(ent, chestSev >= 0.7f ? FishPainSoundKind.Cough : FishPainSoundKind.Gasp);
                ent.Comp.NextPainSound = Timing.CurTime + TimeSpan.FromSeconds(3);
                Dirty(ent);
            }
        }

        // Электротравма: краткая потеря координации.
        if (shock >= 8f)
        {
            _trauma.ApplyDisorient(ent.Owner, TimeSpan.FromSeconds(2 + Math.Min(shock / 5f, 6f)));
            _jitter.DoJitter(ent.Owner, TimeSpan.FromSeconds(1.5), refresh: true, amplitude: 18f, frequency: 9f);
        }

        // Тяжёлые ожоги — слабость / ограничение движения.
        if (heat >= 12f)
            _trauma.ApplyLegImpairment(ent.Owner, Math.Clamp(heat / 60f, 0.08f, 0.35f));
    }

    private static float GetDmg(DamageSpecifier delta, string type)
    {
        return delta.DamageDict.TryGetValue(type, out var v) && v > 0 ? v.Float() : 0f;
    }
}
