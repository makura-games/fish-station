using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Skills;

/// <summary>
/// Атлетика: скорость передвижения и урон ближнего боя.
/// </summary>
public sealed partial class SkillGameplayHooksSystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _moveSpeed = default!;

    partial void InitializeFishMovementHooks()
    {
        SubscribeLocalEvent<SkillsetComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<SkillsetComponent, AfterAutoHandleStateEvent>(OnSkillsetState);
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeDamageEvent>(OnMeleeDamage);
    }

    private void OnMeleeDamage(EntityUid uid, MeleeWeaponComponent component, ref GetMeleeDamageEvent args)
    {
        var qual = _skills.GetSkillQualityMultiplier(args.User, WeaponsSkill);
        if (Math.Abs(qual - 1f) < 0.001f)
            return;

        args.Damage *= qual;
    }

    private void OnRefreshMovementSpeed(Entity<SkillsetComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        // DelayMultiplier < 1 → быстрее; инвертируем в множитель скорости.
        var mult = _skills.GetSkillDelayMultiplier(ent.Owner, AthleticsSkill);
        if (Math.Abs(mult - 1f) < 0.001f)
            return;

        var speed = Math.Clamp(1f / mult, 0.85f, 1.12f);
        args.ModifySpeed(speed);
    }

    private void OnSkillsetState(Entity<SkillsetComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _moveSpeed.RefreshMovementSpeedModifiers(ent.Owner);
    }
}
