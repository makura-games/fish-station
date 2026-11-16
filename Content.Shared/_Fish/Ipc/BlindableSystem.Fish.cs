using Content.Shared.Eye.Blinding.Components;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Eye.Blinding.Systems;

public sealed partial class BlindableSystem
{
    /// <summary>
    /// Накапливает дробное лечение глаз и снимает целые единицы <see cref="BlindableComponent.EyeDamage"/>.
    /// </summary>
    public void HealEyeDamageFractional(Entity<BlindableComponent?> ent, float amount)
    {
        if (amount <= 0f || !Resolve(ent, ref ent.Comp, false))
            return;

        if (ent.Comp.EyeDamage <= ent.Comp.MinDamage)
        {
            if (ent.Comp.EyeHealBuffer != 0f)
            {
                ent.Comp.EyeHealBuffer = 0f;
                Dirty(ent.Owner, ent.Comp);
            }

            return;
        }

        ent.Comp.EyeHealBuffer += amount;
        var healInt = (int) MathF.Floor(ent.Comp.EyeHealBuffer);
        if (healInt <= 0)
        {
            Dirty(ent.Owner, ent.Comp);
            return;
        }

        ent.Comp.EyeHealBuffer -= healInt;
        AdjustEyeDamage(ent, -healInt);
        if (ent.Comp.EyeDamage <= ent.Comp.MinDamage && ent.Comp.EyeHealBuffer != 0f)
        {
            ent.Comp.EyeHealBuffer = 0f;
            Dirty(ent.Owner, ent.Comp);
        }
    }
}
