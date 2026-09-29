using System.Linq;
using Content.Server.Telephone;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Holopad;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Power;
using Content.Shared.Telephone;
using Robust.Shared.GameObjects;

namespace Content.Server.Holopad;

public sealed partial class RemoteHolopadSystem : EntitySystem
{
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private TelephoneSystem _telephone = default!;
    [Dependency] private SharedActionsSystem _actions = default!;

    private const float UpdateInterval = 1f;
    private float _updateTimer;

    private readonly Dictionary<EntityUid, EntityUid?> _previousEyeTargets = new();
    private readonly List<Entity<RemoteHolopadViewerComponent>> _expiredViewers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TelephoneCallAttemptEvent>(OnCallAttempt);

        SubscribeLocalEvent<RemoteHolopadTransmitterComponent, ActivateInWorldEvent>(OnTransmitterActivate);
        SubscribeLocalEvent<RemoteHolopadTransmitterComponent, RemoteHolopadJoinViewMessage>(OnJoinView);
        SubscribeLocalEvent<RemoteHolopadTransmitterComponent, ComponentShutdown>(OnTransmitterShutdown);
        SubscribeLocalEvent<RemoteHolopadTransmitterComponent, PowerChangedEvent>(OnTransmitterPower);

        SubscribeLocalEvent<RemoteHolopadReceiverComponent, ComponentShutdown>(OnReceiverShutdown);
        SubscribeLocalEvent<RemoteHolopadReceiverComponent, PowerChangedEvent>(OnReceiverPower);

        SubscribeLocalEvent<RemoteHolopadViewerComponent, RemoteHolopadExitViewActionEvent>(OnExitViewAction);
        SubscribeLocalEvent<RemoteHolopadViewerComponent, DamageChangedEvent>(OnViewerDamaged);
        SubscribeLocalEvent<RemoteHolopadViewerComponent, PullAttemptEvent>(OnViewerPulled);
        SubscribeLocalEvent<RemoteHolopadViewerComponent, MobStateChangedEvent>(OnViewerMobState);
        SubscribeLocalEvent<RemoteHolopadViewerComponent, ComponentShutdown>(OnViewerShutdown);

    }

    #region: Публичное API

    public bool CanCall(EntityUid source, EntityUid receiver)
    {
        var sourceIsTransmitter = HasComp<RemoteHolopadTransmitterComponent>(source);
        var sourceIsReceiver = HasComp<RemoteHolopadReceiverComponent>(source);
        var receiverIsTransmitter = HasComp<RemoteHolopadTransmitterComponent>(receiver);
        var receiverIsReceiver = HasComp<RemoteHolopadReceiverComponent>(receiver);

        if (!sourceIsTransmitter && !sourceIsReceiver && !receiverIsTransmitter && !receiverIsReceiver)
            return true;

        return sourceIsTransmitter && receiverIsReceiver;
    }

    public bool IsListedFor(EntityUid source, EntityUid receiver) => CanCall(source, receiver);

    #endregion

    #region: Изоляция сети

    private void OnCallAttempt(ref TelephoneCallAttemptEvent ev)
    {
        if (!CanCall(ev.Source, ev.Receiver))
        {
            ev.Cancelled = true;
            return;
        }

        if (HasComp<RemoteHolopadTransmitterComponent>(ev.Source) &&
            TryComp<TelephoneComponent>(ev.Source, out var sourceTelephone) &&
            sourceTelephone.LinkedTelephones.Count > 0)
        {
            ev.Cancelled = true;
        }
    }

    #endregion

    #region: Вход в просмотр

    private void OnTransmitterActivate(Entity<RemoteHolopadTransmitterComponent> ent, ref ActivateInWorldEvent args)
    {
        TryEnterView(ent, args.User);
    }

    private void OnJoinView(Entity<RemoteHolopadTransmitterComponent> ent, ref RemoteHolopadJoinViewMessage args)
    {
        TryEnterView(ent, args.Actor);
    }

    public bool TryEnterView(Entity<RemoteHolopadTransmitterComponent> ent, EntityUid actor)
    {
        if (!CanEnterView(ent, actor, out var receiver))
            return false;

        DoEnterView(ent, actor, receiver);
        return true;
    }

    private bool CanEnterView(Entity<RemoteHolopadTransmitterComponent> ent, EntityUid actor, out EntityUid receiver)
    {
        receiver = default;

        if (TerminatingOrDeleted(actor))
            return false;

        if (!TryComp<TelephoneComponent>(ent, out var telephone) || telephone.CurrentState != TelephoneState.InCall)
            return false;

        if (HasComp<RemoteHolopadViewerComponent>(actor))
            return false;

        if (!HasComp<EyeComponent>(actor))
            return false;

        var linkedReceiver = telephone.LinkedTelephones.FirstOrDefault().Owner;
        if (linkedReceiver == default || TerminatingOrDeleted(linkedReceiver))
            return false;

        receiver = linkedReceiver;
        return true;
    }

    private void DoEnterView(Entity<RemoteHolopadTransmitterComponent> ent, EntityUid actor, EntityUid receiver)
    {
        var eye = Comp<EyeComponent>(actor);
        _previousEyeTargets[actor] = eye.Target;

        ent.Comp.Viewers.Add(actor);

        var viewer = EnsureComp<RemoteHolopadViewerComponent>(actor);
        viewer.Transmitter = ent.Owner;
        _actions.AddAction(actor, ref viewer.ExitViewActionEntity, ent.Comp.ExitViewAction);

        _eye.SetTarget(actor, receiver, eye);

        EnsureComp<RemoteViewerFrozenComponent>(actor);
        _blocker.UpdateCanMove(actor);
    }

    #endregion

    #region: Выход из просмотра

    private void OnExitViewAction(Entity<RemoteHolopadViewerComponent> ent, ref RemoteHolopadExitViewActionEvent args)
    {
        if (args.Handled)
            return;

        if (TryExitView(ent))
            args.Handled = true;
    }

    public bool TryExitView(Entity<RemoteHolopadViewerComponent> ent)
    {
        if (!CanExitView(ent))
            return false;

        DoExitView(ent);
        return true;
    }

    private bool CanExitView(Entity<RemoteHolopadViewerComponent> ent)
    {
        return ent.Comp.LifeStage <= ComponentLifeStage.Running && !TerminatingOrDeleted(ent.Owner);
    }

    private void DoExitView(Entity<RemoteHolopadViewerComponent> ent)
    {
        if (TryComp<RemoteHolopadTransmitterComponent>(ent.Comp.Transmitter, out var transmitter))
            transmitter.Viewers.Remove(ent.Owner);

        _previousEyeTargets.Remove(ent.Owner, out var previousTarget);

        if (TerminatingOrDeleted(ent.Owner))
            return;

        if (previousTarget is { } target && TerminatingOrDeleted(target))
            previousTarget = null;

        if (TryComp<EyeComponent>(ent.Owner, out var eye))
            _eye.SetTarget(ent.Owner, previousTarget, eye);

        RemComp<RemoteViewerFrozenComponent>(ent.Owner);

        _actions.RemoveAction(ent.Owner, ent.Comp.ExitViewActionEntity);
        RemComp<RemoteHolopadViewerComponent>(ent.Owner);
    }

    #endregion

    #region: Аварийные выходы

    private void OnTransmitterShutdown(Entity<RemoteHolopadTransmitterComponent> ent, ref ComponentShutdown args)
    {
        foreach (var viewer in ent.Comp.Viewers.ToArray())
        {
            if (TryComp<RemoteHolopadViewerComponent>(viewer, out var viewerComp))
                DoExitView((viewer, viewerComp));
        }
    }

    private void OnTransmitterPower(Entity<RemoteHolopadTransmitterComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            EndCalls(ent.Owner);
    }

    private void OnReceiverShutdown(Entity<RemoteHolopadReceiverComponent> ent, ref ComponentShutdown args)
    {
        EndCalls(ent.Owner);
    }

    private void OnReceiverPower(Entity<RemoteHolopadReceiverComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            EndCalls(ent.Owner);
    }

    private void EndCalls(EntityUid uid)
    {
        if (!TryComp<TelephoneComponent>(uid, out var telephone) ||
            !_telephone.IsTelephoneEngaged((uid, telephone)))
            return;

        _telephone.EndTelephoneCalls((uid, telephone));
    }

    #endregion

    #region: Прерывания просмотра

    private void OnViewerDamaged(Entity<RemoteHolopadViewerComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageIncreased)
            DoExitView(ent);
    }

    private void OnViewerPulled(Entity<RemoteHolopadViewerComponent> ent, ref PullAttemptEvent args)
    {
        DoExitView(ent);
    }

    private void OnViewerMobState(Entity<RemoteHolopadViewerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            DoExitView(ent);
    }

    private void OnViewerShutdown(Entity<RemoteHolopadViewerComponent> ent, ref ComponentShutdown args)
    {
        _previousEyeTargets.Remove(ent.Owner);

        if (TryComp<RemoteHolopadTransmitterComponent>(ent.Comp.Transmitter, out var transmitter))
            transmitter.Viewers.Remove(ent.Owner);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;

        if (_updateTimer < UpdateInterval)
            return;

        _updateTimer -= UpdateInterval;

        _expiredViewers.Clear();

        var query = EntityQueryEnumerator<RemoteHolopadViewerComponent>();
        while (query.MoveNext(out var uid, out var viewerComp))
        {
            if (!TryComp<TelephoneComponent>(viewerComp.Transmitter, out var telephone) ||
                telephone.CurrentState != TelephoneState.InCall)
            {
                _expiredViewers.Add((uid, viewerComp));
            }
        }

        foreach (var viewer in _expiredViewers)
        {
            DoExitView(viewer);
        }

        _expiredViewers.Clear();
    }
}
