using Content.Shared._Starlight.Medical.Damage;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Silicons.Borgs;

/// <summary>
/// Конвертирует входящий Structural урон по киборгам в Blunt, чтобы он не копился отдельно без отображения и ремонта.
/// </summary>
public sealed class FishBorgStructuralDamageSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> StructuralDamageType = "Structural";
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgChassisComponent, DamageBeforeApplyEvent>(OnDamageBeforeApply);
    }

    private static void OnDamageBeforeApply(Entity<BorgChassisComponent> ent, ref DamageBeforeApplyEvent args)
    {
        if (!args.Damage.DamageDict.TryGetValue(StructuralDamageType, out var structural) ||
            structural <= FixedPoint2.Zero)
            return;

        var converted = new DamageSpecifier(args.Damage);
        converted.DamageDict.Remove(StructuralDamageType);
        converted.DamageDict[BluntDamageType] = converted.DamageDict.GetValueOrDefault(BluntDamageType) + structural;
        args.Damage = converted;
    }
}
