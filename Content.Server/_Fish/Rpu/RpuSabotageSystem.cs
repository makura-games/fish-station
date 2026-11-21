using Content.Server.Atmos.Piping.Unary.Components;
using Content.Shared._Fish.Rpu;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Construction.Components;
using Robust.Shared.Player;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Саботаж жизнеобеспечения: снятые с креплений венты, клапаны, баллоны и трубы.
/// Инженерный отдел освобождён полностью — для него это работа.
/// </summary>
public sealed partial class RpuSabotageSystem : EntitySystem
{
    [Dependency] private RpuSystem _rpu = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GasVentPumpComponent, BeforeUnanchoredEvent>((_, __, args) => Charge(args.User));
        SubscribeLocalEvent<GasVentScrubberComponent, BeforeUnanchoredEvent>((_, __, args) => Charge(args.User));
        SubscribeLocalEvent<GasPassiveVentComponent, BeforeUnanchoredEvent>((_, __, args) => Charge(args.User));
        SubscribeLocalEvent<GasCanisterComponent, BeforeUnanchoredEvent>((_, __, args) => Charge(args.User));
    }

    private void Charge(EntityUid user)
    {
        if (!_rpu.Active)
            return;

        if (CompOrNull<ActorComponent>(user)?.PlayerSession is not { } session)
            return;

        _rpu.Penalize(session, RpuPrototypeIds.SabotageAtmos, details: "life support tampering");
    }
}
