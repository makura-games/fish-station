using Content.Shared._Fish.Ipc;
using Content.Shared._Fish.Ipc.Modules;
using Content.Shared.Emp;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Ipc.Modules;

/// <summary>
/// Магнитные/герметичные суставы: сопротивление ампутации и реакция на EMP.
/// </summary>
public sealed partial class FishIpcJointsSystem : EntitySystem
{
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Событие поднимается на Body из SurgerySystem (Fish-edit) — не дублируем Comp+Event Surgery.
        SubscribeLocalEvent<FishIpcSealedJointsHostComponent, FishIpcAmputationAttemptEvent>(OnSealedAmputation);
        SubscribeLocalEvent<FishIpcMagneticJointsHostComponent, FishIpcAmputationAttemptEvent>(OnMagneticAmputation);

        SubscribeLocalEvent<FishIpcMagneticJointsHostComponent, EmpPulseEvent>(OnMagEmp);
        SubscribeLocalEvent<FishIpcSealedJointsHostComponent, EmpPulseEvent>(OnSealEmp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var mag = EntityQueryEnumerator<FishIpcMagneticJointsHostComponent>();
        while (mag.MoveNext(out var uid, out var comp))
        {
            if (!comp.EmpDisabled || now < comp.EmpDisabledUntil)
                continue;

            comp.EmpDisabled = false;
            Dirty(uid, comp);
        }

        var sealedQ = EntityQueryEnumerator<FishIpcSealedJointsHostComponent>();
        while (sealedQ.MoveNext(out var uid, out var comp))
        {
            if (!comp.EmpDisabled || now < comp.EmpDisabledUntil)
                continue;

            comp.EmpDisabled = false;
            Dirty(uid, comp);
        }
    }

    private void OnSealedAmputation(Entity<FishIpcSealedJointsHostComponent> ent, ref FishIpcAmputationAttemptEvent args)
    {
        if (args.Cancelled || ent.Comp.EmpDisabled || !ent.Comp.ModuleEnabled || !ChassisPowered(ent.Owner))
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("fish-ipc-module-joints-sealed-block"), ent, args.User, PopupType.MediumCaution);
    }

    private void OnMagneticAmputation(Entity<FishIpcMagneticJointsHostComponent> ent, ref FishIpcAmputationAttemptEvent args)
    {
        if (args.Cancelled || ent.Comp.EmpDisabled || !ent.Comp.ModuleEnabled || !ChassisPowered(ent.Owner))
            return;

        if (!_random.Prob(ent.Comp.ResistChance))
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("fish-ipc-module-joints-magnetic-resist"), ent, args.User, PopupType.SmallCaution);
    }

    private void OnMagEmp(Entity<FishIpcMagneticJointsHostComponent> ent, ref EmpPulseEvent args)
    {
        args.Affected = true;
        ent.Comp.EmpDisabled = true;
        ent.Comp.EmpDisabledUntil = _timing.CurTime + args.Duration + TimeSpan.FromSeconds(8);
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("fish-ipc-module-joints-emp-magnetic"), ent, ent, PopupType.MediumCaution);
    }

    private void OnSealEmp(Entity<FishIpcSealedJointsHostComponent> ent, ref EmpPulseEvent args)
    {
        args.Affected = true;
        ent.Comp.EmpDisabled = true;
        ent.Comp.EmpDisabledUntil = _timing.CurTime + args.Duration + TimeSpan.FromSeconds(15);
        Dirty(ent);
        _stun.TryUpdateStunDuration(ent.Owner, ent.Comp.EmpStunDuration);
        _popup.PopupEntity(Loc.GetString("fish-ipc-module-joints-emp-sealed"), ent, ent, PopupType.LargeCaution);
    }

    private bool ChassisPowered(EntityUid uid)
    {
        return !TryComp<FishIpcComponent>(uid, out var ipc) || !ipc.PowerDown;
    }
}
