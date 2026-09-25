using System.Linq;
using Content.Server.Telephone;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Holopad;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Power;
using Content.Shared.Telephone;
using Robust.Shared.GameObjects;

namespace Content.Server.Holopad;

public sealed class RemoteHolopadSystem : EntitySystem
{
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly TelephoneSystem _telephone = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    private const float UpdateInterval = 1f;
    private float _updateTimer;

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

        SubscribeLocalEvent<RemoteViewerFrozenComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<RemoteViewerFrozenComponent, ComponentShutdown>(OnFrozenShutdown);
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

    private void TryEnterView(Entity<RemoteHolopadTransmitterComponent> ent, EntityUid actor)
    {
        if (!TryComp<TelephoneComponent>(ent, out var telephone) || telephone.CurrentState != TelephoneState.InCall)
            return;

        if (HasComp<RemoteHolopadViewerComponent>(actor))
            return;

        var receiver = telephone.LinkedTelephones.FirstOrDefault().Owner;
        if (receiver == default)
            return;

        ent.Comp.Viewers.Add(actor);

        var viewer = EnsureComp<RemoteHolopadViewerComponent>(actor);
        viewer.Transmitter = ent.Owner;
        _actions.AddAction(actor, ref viewer.ExitViewActionEntity, ent.Comp.ExitViewAction);

        if (TryComp<EyeComponent>(actor, out var eye))
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

        args.Handled = true;
        ExitView(ent);
    }

    private void ExitView(Entity<RemoteHolopadViewerComponent> ent)
    {
        if (TryComp<RemoteHolopadTransmitterComponent>(ent.Comp.Transmitter, out var transmitter))
            transmitter.Viewers.Remove(ent.Owner);

        if (TryComp<EyeComponent>(ent.Owner, out var eye))
            _eye.SetTarget(ent.Owner, null, eye);

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
                ExitView((viewer, viewerComp));
        }
    }

    private void OnTransmitterPower(Entity<RemoteHolopadTransmitterComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered && TryComp<TelephoneComponent>(ent, out var tel))
            _telephone.EndTelephoneCalls((ent.Owner, tel));
    }

    private void OnReceiverShutdown(Entity<RemoteHolopadReceiverComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<TelephoneComponent>(ent, out var tel) && _telephone.IsTelephoneEngaged((ent.Owner, tel)))
            _telephone.EndTelephoneCalls((ent.Owner, tel));
    }

    private void OnReceiverPower(Entity<RemoteHolopadReceiverComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered && TryComp<TelephoneComponent>(ent, out var tel))
            _telephone.EndTelephoneCalls((ent.Owner, tel));
    }

    #endregion

    #region: Заморозка

    private void OnUpdateCanMove(Entity<RemoteViewerFrozenComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        args.Cancel();
    }

    private void OnFrozenShutdown(Entity<RemoteViewerFrozenComponent> ent, ref ComponentShutdown args)
    {
        _blocker.UpdateCanMove(ent.Owner);
    }

    #endregion

    #region: Прерывания просмотра

    private void OnViewerDamaged(Entity<RemoteHolopadViewerComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageIncreased)
            ExitView(ent);
    }

    private void OnViewerPulled(Entity<RemoteHolopadViewerComponent> ent, ref PullAttemptEvent args)
    {
        ExitView(ent);
    }

    private void OnViewerMobState(Entity<RemoteHolopadViewerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            ExitView(ent);
    }

    private void OnViewerShutdown(Entity<RemoteHolopadViewerComponent> ent, ref ComponentShutdown args)
    {
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

        var query = EntityQueryEnumerator<RemoteHolopadViewerComponent>();
        while (query.MoveNext(out var uid, out var viewerComp))
        {
            if (!TryComp<TelephoneComponent>(viewerComp.Transmitter, out var telephone) ||
                telephone.CurrentState != TelephoneState.InCall)
            {
                ExitView((uid, viewerComp));
            }
        }
    }
}
