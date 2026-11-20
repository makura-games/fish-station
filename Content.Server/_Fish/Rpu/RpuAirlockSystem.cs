using Content.Server._Fish.Rpu;
using Content.Shared._Fish.Rpu;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Emag.Systems;
using Content.Server.Wires;
using Content.Shared.Wires;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Взлом шлюзов и перерезанные провода. Пара (AccessReaderComponent, GotEmaggedEvent)
/// занята движком, (WiresComponent, WireDoAfterEvent) — проводовщиком, поэтому подписываемся
/// на те же события по компоненту шлюза: нам именно двери и интересны.
/// Шлюз командования, СБ и ЦК наказывается сильно, бесправный — слабо.
/// </summary>
public sealed partial class RpuAirlockSystem : EntitySystem
{
    [Dependency] private RpuSystem _rpu = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Ждём, пока отработают дверь и читалка доступов: нам важно, что взлом удался
        // (args.Handled), а это решают они.
        SubscribeLocalEvent<AirlockComponent, GotEmaggedEvent>(OnEmagged,
            after: [typeof(SharedDoorSystem), typeof(AccessReaderSystem)]);
        SubscribeLocalEvent<AirlockComponent, WireDoAfterEvent>(OnWireDoAfter,
            after: [typeof(WiresSystem)]);
    }

    private void OnEmagged(Entity<AirlockComponent> ent, ref GotEmaggedEvent args)
    {
        if (!args.Handled)
            return;

        Charge(args.UserUid, ent.Owner, AirlockViolation(ent.Owner));
    }

    private void OnWireDoAfter(Entity<AirlockComponent> ent, ref WireDoAfterEvent args)
    {
        if (args.Cancelled || args.Action != WiresAction.Cut)
            return;

        // Перерезанный провод шлюза — тот же уровень взлома, что и emag: цена по доступам двери.
        Charge(args.User, ent.Owner, AirlockViolation(ent.Owner));
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
