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
using Content.Shared.Slippery;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
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

        Charge(args.UserUid, ent.Owner, AirlockViolation(ent.Owner, args.UserUid));
    }

    private void OnWireDoAfter(Entity<AirlockComponent> ent, ref WireDoAfterEvent args)
    {
        if (args.Cancelled || args.Action != WiresAction.Cut)
            return;

        // Перерезанный провод шлюза — тот же уровень взлома, что и emag: цена по доступам двери.
        Charge(args.User, ent.Owner, AirlockViolation(ent.Owner, args.User));
    }

    /// <summary>Насколько «жирный» шлюз взломали.</summary>
    private ProtoId<RpuViolationPrototype> AirlockViolation(EntityUid uid, EntityUid user)
    {
        var accesses = CollectAccesses(uid, user);

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

    private HashSet<string> CollectAccesses(EntityUid uid, EntityUid user)
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

        // Взлом оценивается и по тому, чьи допуски при этом светились: карта может лежать
        // в слоте, который читалка дверей не перебирает, а взломщик её всё равно показал.
        if (CompOrNull<AccessComponent>(user) is { } card)
        {
            foreach (var tag in card.Tags)
                result.Add(tag.ToString());
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
