using Content.Shared._Fish.Rpu;
using Content.Shared.Doors.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Slippery;
using Content.Shared.Throwing;
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
using Content.Shared.Wires;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;
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

        // Отвечает игрок, а не тело: тело могут освободить, занять или пересоздать,
        // а наказан должен быть тот, кто положил кожуру.
        EntityUid? body = null;
        NetUserId? player = null;

        if (user != null && TryComp<ActorComponent>(user.Value, out var actor))
        {
            body = user;
            player = actor.PlayerSession.UserId;
        }

        culprit.Culprit = body;
        culprit.CulpritUser = player;
    }

    private void OnSlip(Entity<SlipperyComponent> ent, ref SlipEvent args)
    {
        if (!_rpu.Active)
            return;

        if (!TryComp<RpuSlipCulpritComponent>(ent.Owner, out var culprit) || culprit.CulpritUser is not { } culpritUser)
            return;

        // Сам виновник на своей кожуре — не нарушение. Сравниваем игроков, а не тела:
        // к моменту скольжения в этом теле мог сидеть совсем другой человек.
        if (CompOrNull<ActorComponent>(args.Slipped)?.PlayerSession.UserId == culpritUser)
            return;

        // Наказываем того, кто положил предмет, даже если он уже вышел из этого тела.
        if (!_rpu.TryGetSession(culpritUser, out var session))
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

    /// <summary>Игрок, который положил или бросил предмет. Именно он отвечает за скольжение.</summary>
    [ViewVariables]
    public NetUserId? CulpritUser;
}
