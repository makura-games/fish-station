using Content.Server._Fish.Rpu;
using Content.Shared._Fish.Rpu;
using Content.Shared.Access.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Emag.Systems;
using Content.Server.Wires;
using Content.Shared.Wires;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Взлом шлюзов и перерезанные провода.
/// Шлюз командования, СБ и ЦК наказывается сильно, бесправный — слабо.
/// </summary>
public sealed partial class RpuAirlockSystem : EntitySystem
{
    [Dependency] private RpuSystem _rpu = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AccessReaderComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<AirlockComponent, DoorEmaggedEvent>(OnAirlockEmagged);
        SubscribeLocalEvent<WiresComponent, WireDoAfterEvent>(OnWireDoAfter, after: [typeof(WiresSystem)]);
    }

    private void OnAirlockEmagged(EntityUid uid, AirlockComponent component, DoorEmaggedEvent args)
    {
        Charge(args.UserUid, uid, AirlockViolation(uid));
    }

    private void OnEmagged(EntityUid uid, AccessReaderComponent component, ref GotEmaggedEvent args)
    {
        if (!args.Handled)
            return;

        // Emag по двери — это взлом шлюза; по остальному оборудованию считаем возней с проводами.
        Charge(args.UserUid, uid, HasComp<DoorComponent>(uid) ? AirlockViolation(uid) : RpuPrototypeIds.WireCut);
    }

    private void OnWireDoAfter(EntityUid uid, WiresComponent component, WireDoAfterEvent args)
    {
        if (args.Cancelled || args.Action != WiresAction.Cut)
            return;

        var isAirlock = HasComp<AirlockComponent>(uid) || HasComp<DoorBoltComponent>(uid);

        Charge(args.User, uid, isAirlock ? AirlockViolation(uid) : RpuPrototypeIds.WireCut);
    }

    /// <summary>Насколько «жирный» шлюз взломали.</summary>
    private ProtoId<RpuViolationPrototype> AirlockViolation(EntityUid uid)
    {
        var accesses = CollectAccesses(uid);

        if (accesses.Count == 0)
            return RpuPrototypeIds.AirlockBasic;

        var tuning = _rpu.Tuning;

        if (accesses.Overlaps(tuning.HighSecurityAccess))
            return RpuPrototypeIds.AirlockHighSecurity;

        if (accesses.Overlaps(tuning.DepartmentAccess))
            return RpuPrototypeIds.AirlockDepartment;

        if (accesses.Overlaps(tuning.ServiceAccess))
            return RpuPrototypeIds.AirlockService;

        return RpuPrototypeIds.AirlockBasic;
    }

    private HashSet<string> CollectAccesses(EntityUid uid)
    {
        var result = new HashSet<string>();

        if (TryComp<AccessReaderComponent>(uid, out var reader))
        {
            if (reader.Group is { } group)
                result.Add(group.ToString());

            foreach (var list in reader.AccessLists)
            {
                foreach (var access in list)
                    result.Add(access.ToString());
            }
        }

        return result;
    }

    private void Charge(EntityUid user, EntityUid door, ProtoId<RpuViolationPrototype> violation)
    {
        if (!_rpu.Active)
            return;

        if (CompOrNull<ActorComponent>(user)?.PlayerSession is not { } session)
            return;

        _rpu.Penalize(session, violation, details: "door breach");
    }
}
