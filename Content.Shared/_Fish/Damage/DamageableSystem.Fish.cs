using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Damage.Systems;

/// <summary>
/// Чтение одного типа урона без клонирования всего <see cref="DamageSpecifier"/>.
/// </summary>
public sealed partial class DamageableSystem
{
    /// <summary>
    /// Возвращает текущий урон типа <paramref name="type"/> без копии словаря.
    /// </summary>
    public bool TryGetDamage(Entity<DamageableComponent> ent, ProtoId<DamageTypePrototype> type, out FixedPoint2 value)
    {
        return ent.Comp.Damage.DamageDict.TryGetValue(type, out value);
    }
}
#pragma warning restore IDE0130
