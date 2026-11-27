using Content.Server.Atmos.Piping.Unary.Components;
using Content.Shared._Fish.Rpu;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Construction.Components;
using Robust.Shared.Player;

using System.Linq;
using Robust.Shared.Enums;
using Robust.Shared.Audio.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
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
using Robust.Shared.Random;
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
