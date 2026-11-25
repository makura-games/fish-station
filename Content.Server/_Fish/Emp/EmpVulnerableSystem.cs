using Content.Shared._Fish.Emp;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Emp;

/// <summary>
/// Наносит урон сущностям с <see cref="EmpVulnerableComponent"/> при получении ЭМИ-импульса.
/// </summary>
public sealed partial class EmpVulnerableSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmpVulnerableComponent, EmpPulseEvent>(OnEmpPulse);
    }

    private void OnEmpPulse(Entity<EmpVulnerableComponent> ent, ref EmpPulseEvent args)
    {
        var curTime = _timing.CurTime;
        if (curTime < ent.Comp.NextPulseTime)
            return;

        if (ent.Comp.Cooldown > TimeSpan.Zero)
            ent.Comp.NextPulseTime = curTime + ent.Comp.Cooldown;

        if (ent.Comp.TriggerEmpEffect)
            args.Affected = true;

        _damageable.TryChangeDamage(ent.Owner, ent.Comp.Damage, origin: args.User);
    }
}
