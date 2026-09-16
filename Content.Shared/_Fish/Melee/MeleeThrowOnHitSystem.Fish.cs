using Content.Shared.Atmos.Components;
using Content.Shared.Weapons.Melee.Components;

// Fish edit - расширение MeleeThrowOnHitSystem для Fish-форка
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Weapons.Melee;

public sealed partial class MeleeThrowOnHitSystem
{
    // Fish edit start - иммунитет к отбрасыванию для магнитных ботинок
    private partial bool ShouldApplyThrow(Entity<MeleeThrowOnHitComponent> ent, EntityUid target)
    {
        if (HasComp<ThrowOnHitMagbootsImmuneComponent>(ent.Owner)
            && TryComp<MovedByPressureComponent>(target, out var moved)
            && !moved.Enabled)
            return false;

        return true;
    }
    // Fish edit end
}
