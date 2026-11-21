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
    [Dependency] private RpuAdjustSystem _mods = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlipperyComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<SlipperyComponent, ThrowEvent>(OnThrown);
        SubscribeLocalEvent<SlipperyComponent, SlipEvent>(OnSlip);
    }

    private void OnDropped(Entity<SlipperyComponent> ent, ref DroppedEvent args)
    {
        Mark(ent.Owner, args.User);
    }

    private void OnThrown(Entity<SlipperyComponent> ent, ref ThrowEvent args)
    {
        Mark(ent.Owner, args.User);
    }

    /// <summary>
    /// Ответственность всегда у того, кто последним положил или бросил предмет.
    /// Если это сделал не игрок (шахтёр-бот, конвейер, выпавший из рук хлам),
    /// прежний виновник снимается: с человека за чужие действия карму не берём.
    /// </summary>
    private void Mark(EntityUid uid, EntityUid? user)
    {
        var culprit = EnsureComp<RpuSlipCulpritComponent>(uid);
        culprit.Culprit = user != null && HasComp<ActorComponent>(user.Value) ? user : null;
    }

    private void OnSlip(Entity<SlipperyComponent> ent, ref SlipEvent args)
    {
        if (!_rpu.Active)
            return;

        if (!TryComp<RpuSlipCulpritComponent>(ent.Owner, out var culprit) || culprit.Culprit == null)
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
/// Обнуляется, когда предмет положил или бросил не игрок.
/// </summary>
[RegisterComponent]
public sealed partial class RpuSlipCulpritComponent : Component
{
    [ViewVariables]
    public EntityUid? Culprit;
}
