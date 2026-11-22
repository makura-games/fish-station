using Content.Server._Fish.PerformanceMetrics;
using Content.Shared._Fish.Rpu;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;

using System.Linq;
using Robust.Shared.Enums;
using Robust.Shared.Audio.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Gibbing;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Throwing;
using Content.Shared.Wires;
using Content.Shared.Slippery;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
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
    [Dependency] private Content.Server._Fish.PerformanceMetrics.PmSystem _pm = default!;
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
        // занята, поэтому данные раздаём внутренним уведомлением. Пока сбор выключен,
        // событие не отправляем вовсе: лишний объект на каждый удар никому не нужен.
        if (_pm.Enabled)
            RaiseLocalEvent(new PmDamageObservedEvent { Victim = ent.Owner, Source = origin, Total = (float) total });
    }
}
