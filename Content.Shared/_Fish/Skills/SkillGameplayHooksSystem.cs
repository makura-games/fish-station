using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Skills;

/// <summary>
/// Геймплей-хуки: оружие (темп/разброс) и атлетика (скорость ближнего боя).
/// </summary>
public sealed partial class SkillGameplayHooksSystem : EntitySystem
{
    [Dependency] private SharedSkillSystem _skills = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private static readonly ProtoId<SkillPrototype> WeaponsSkill = "FishSkillWeapons";
    private static readonly ProtoId<SkillPrototype> AthleticsSkill = "FishSkillAthletics";

    partial void InitializeFishMovementHooks();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GunComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeAttackRateEvent>(OnGetMeleeAttackRate);
        InitializeFishMovementHooks();
    }

    private void OnGunRefreshModifiers(Entity<GunComponent> gun, ref GunRefreshModifiersEvent args)
    {
        if (!TryGetGunUser(gun, out var user))
            return;

        // delayMult < 1 — быстрее; > 1 — медленнее и больше разброс/отдача.
        var delayMult = _skills.GetSkillDelayMultiplier(user, WeaponsSkill);
        if (delayMult <= 0.01f)
            return;

        args.FireRate /= delayMult;
        args.AngleIncrease *= delayMult;
        args.MaxAngle *= delayMult;
        args.CameraRecoilScalar *= delayMult;
    }

    private void OnGetMeleeAttackRate(EntityUid uid, MeleeWeaponComponent component, ref GetMeleeAttackRateEvent args)
    {
        var delayMult = _skills.GetSkillDelayMultiplier(args.User, WeaponsSkill);
        if (delayMult <= 0.01f)
            return;

        // Rate * Multipliers: повышаем Multipliers при хорошем навыке.
        args.Multipliers /= delayMult;
    }

    private bool TryGetGunUser(EntityUid gun, out EntityUid user)
    {
        user = default;
        var parent = _transform.GetParentUid(gun);
        if (!parent.IsValid())
            return false;

        if (_hands.IsHolding(parent, gun) || HasComp<SkillsetComponent>(parent))
        {
            user = parent;
            return true;
        }

        return false;
    }
}
