using Content.Shared._Fish.Rpu;
using Content.Shared.Doors.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Slippery;
using Content.Shared.Throwing;
using Robust.Shared.Player;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Кожура и мыло. Виновником считаем того, кто положил или бросил предмет:
/// штраф идёт за каждую подскользнувшуюся на нём жертву.
/// </summary>
public sealed partial class RpuSlipSystem : EntitySystem
{
    [Dependency] private RpuSystem _rpu = default!;
    [Dependency] private RpuModifierSystem _mods = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlipperyComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<SlipperyComponent, ThrowEvent>(OnThrown);
        SubscribeLocalEvent<SlipperyComponent, SlipEvent>(OnSlip);
    }

    private void OnDropped(EntityUid uid, SlipperyComponent component, DroppedEvent args)
    {
        Mark(uid, args.User);
    }

    private void OnThrown(EntityUid uid, SlipperyComponent component, ThrowEvent args)
    {
        if (args.User is { } user)
            Mark(uid, user);
    }

    private void Mark(EntityUid uid, EntityUid user)
    {
        if (CompOrNull<ActorComponent>(user) == null)
            return;

        var culprit = EnsureComp<RpuSlipCulpritComponent>(uid);
        culprit.Culprit = user;
    }

    private void OnSlip(EntityUid uid, SlipperyComponent component, SlipEvent args)
    {
        if (!_rpu.Active)
            return;

        if (!TryComp<RpuSlipCulpritComponent>(uid, out var culprit) || culprit.Culprit == null)
            return;

        var culpritEntity = culprit.Culprit.Value;

        // Сам виновник на своей кожуре — не нарушение.
        if (culpritEntity == args.Slipped)
            return;

        if (CompOrNull<ActorComponent>(culpritEntity)?.PlayerSession is not { } session)
            return;

        var violation = IsVip(args.Slipped)
            ? RpuPrototypeIds.SlipVip
            : RpuPrototypeIds.SlipCrew;

        _rpu.Penalize(session, violation, details: "left a slip hazard");
    }

    private bool IsVip(EntityUid victim)
    {
        var tuning = _rpu.Tuning;

        if (!_mods.TryGetRolesByEntity(victim, out var roles))
            return false;

        if (roles.Job != null && tuning.SecurityChainJobs.Contains(roles.Job.Value))
            return true;

        return roles.Departments.Any(department => tuning.SecurityChainDepartments.Contains(department));
    }
}

/// <summary>
/// Серверная пометка: кто ответил за то, что скользкий предмет оказался на полу.
/// </summary>
[RegisterComponent]
public sealed partial class RpuSlipCulpritComponent : Component
{
    [ViewVariables]
    public EntityUid? Culprit;
}
