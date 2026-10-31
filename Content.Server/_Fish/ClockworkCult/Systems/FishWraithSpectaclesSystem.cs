using Content.Shared._Fish.ClockworkCult.Components;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.ClockworkCult.Systems;

/// <summary>
/// Wraith Spectacles: урон носителю пока надеты (кроме Heralded), лёгкий heal при снятии.
/// </summary>
public sealed partial class FishWraithSpectaclesSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private FishClockworkPowerSystem _power = default!;

    private static readonly ProtoId<DamageTypePrototype> BluntDamage = "Blunt";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishWraithSpectaclesComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<FishWraithSpectaclesComponent, ClothingGotUnequippedEvent>(OnUnequipped);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Fish-edit: IsHeralded один раз за тик; без Dirty каждый кадр.
        var heralded = _power.IsHeralded();
        var query = EntityQueryEnumerator<FishWraithSpectaclesComponent>();
        while (query.MoveNext(out var uid, out var specs))
        {
            if (specs.Wearer is not { } wearer || !Exists(wearer))
            {
                // Не надеты — постепенно «лечим» накопленный урон (метаданные).
                if (specs.AccumulatedEyeDamage > 0f)
                    specs.AccumulatedEyeDamage = MathF.Max(0f, specs.AccumulatedEyeDamage - specs.HealPerSecondUnequipped * frameTime);

                continue;
            }

            // Heralded — без урона глазам (как на tg после herald).
            if (heralded)
                continue;

            var amount = specs.EyeDamagePerSecond * frameTime;
            if (amount <= 0f)
                continue;

            var dmg = new DamageSpecifier();
            dmg.DamageDict[BluntDamage] = amount;
            if (_damageable.TryChangeDamage(wearer, dmg, origin: uid))
                specs.AccumulatedEyeDamage += amount;
        }
    }

    private void OnEquipped(Entity<FishWraithSpectaclesComponent> ent, ref ClothingGotEquippedEvent args)
    {
        ent.Comp.Wearer = args.Wearer;
    }

    private void OnUnequipped(Entity<FishWraithSpectaclesComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        // При снятии слегка лечим накопленный blunt.
        if (ent.Comp.AccumulatedEyeDamage > 0.1f && Exists(args.Wearer))
        {
            var heal = MathF.Min(ent.Comp.AccumulatedEyeDamage, 5f);
            var dmg = new DamageSpecifier();
            dmg.DamageDict[BluntDamage] = -heal;
            _damageable.TryChangeDamage(args.Wearer, dmg);
            ent.Comp.AccumulatedEyeDamage = MathF.Max(0f, ent.Comp.AccumulatedEyeDamage - heal);
        }

        ent.Comp.Wearer = null;
    }
}
