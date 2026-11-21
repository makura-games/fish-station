using Content.Server._Fish.PerformanceMetrics;
using Content.Shared._Fish.Rpu;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Движок разрешает только одну направленную подписку на пару (компонент, событие).
/// Урон экипажа нужен сразу трём подсистемам режима, поэтому его ловит один маршрутизатор
/// и раздаёт дальше. Сам он ничего не считает.
/// </summary>
public sealed partial class RpuDamageRouterSystem : EntitySystem
{
    [Dependency] private RpuAggressionSystem _aggression = default!;
    [Dependency] private RpuKillSystem _kill = default!;
    [Dependency] private RpuDestructionSystem _destruction = default!;
    [Dependency] private RpuSystem _rpu = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageableComponent, DamageDealtEvent>(OnDamageDealt);
    }

    private void OnDamageDealt(Entity<DamageableComponent> ent, ref DamageDealtEvent args)
    {
        var total = args.Damage.GetTotal();
        var origin = args.Origin;

        // Память о нападении нужна и при выключенной системе: включат её позже,
        // а основания для ответного огня должны сохраниться.
        _aggression.RecordDamage(ent.Owner, origin, total);

        if (!_rpu.Active)
            return;

        _kill.RecordDamage(ent.Owner, origin, total);
        _destruction.RecordDamage(ent.Owner, origin, total);

        // Метрики боя берут те же данные: отдельная направленная подписка на эту пару
        // занята, поэтому данные раздаём внутренним уведомлением.
        RaiseLocalEvent(new PmDamageObservedEvent { Victim = ent.Owner, Source = origin, Total = (float) total });
    }
}
