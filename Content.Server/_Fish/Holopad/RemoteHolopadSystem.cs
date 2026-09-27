using System.Linq;
using Content.Server.Power.EntitySystems;
using Content.Server.Telephone;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Holopad;
using Content.Shared.Mobs;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Power;
using Content.Shared.Telephone;
using Robust.Shared.GameObjects;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;


namespace Content.Server.Holopad;

/// <summary>
/// Реализует «транслирующие» голопады: звонящий видит мир от лица голограммы на приёмнике,
/// оставаясь при этом в собственном теле.
/// </summary>
public sealed class RemoteHolopadSystem : EntitySystem
{
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly TelephoneSystem _telephone = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Изоляция: нельзя смешивать кастомные и обычные голопады.
        SubscribeLocalEvent<TelephoneCallAttemptEvent>(OnCallAttempt);

        // Ручное завершение трансляции по кнопке.
        SubscribeLocalEvent<RemoteHolopadTransmitterComponent, RemoteHolopadStopBroadcastMessage>(OnStopBroadcast);

        // Аварийные выходы.
        SubscribeLocalEvent<RemoteHolopadTransmitterComponent, ComponentShutdown>(OnTransmitterShutdown);
        SubscribeLocalEvent<RemoteHolopadTransmitterComponent, PowerChangedEvent>(OnTransmitterPower);

        // Заморозка.
        SubscribeLocalEvent<RemoteViewerFrozenComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<RemoteViewerFrozenComponent, ComponentShutdown>(OnFrozenShutdown);

        // Прерывание при агрессии.
        SubscribeLocalEvent<RemoteViewerFrozenComponent, DamageChangedEvent>(OnViewerDamaged);
        SubscribeLocalEvent<RemoteViewerFrozenComponent, PullAttemptEvent>(OnViewerPulled);
        SubscribeLocalEvent<RemoteViewerFrozenComponent, MobStateChangedEvent>(OnViewerMobState);
    }

    // ---------- Изоляция ----------

    private bool IsRemote(EntityUid uid)
    {
        return HasComp<RemoteHolopadTransmitterComponent>(uid) ||
               HasComp<RemoteHolopadReceiverComponent>(uid);
    }

    private void OnCallAttempt(ref TelephoneCallAttemptEvent ev)
    {
        if (IsRemote(ev.Source) != IsRemote(ev.Receiver))
            ev.Cancelled = true;
    }

    // ---------- Основная логика ----------

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RemoteHolopadTransmitterComponent, HolopadComponent, TelephoneComponent>();
        while (query.MoveNext(out var uid, out var transmitter, out var holopad, out var telephone))
        {
            if (telephone.CurrentState != TelephoneState.InCall || holopad.User == null)
            {
                Cleanup((uid, transmitter));
                continue;
            }

            var viewer = holopad.User.Value.Owner;
            var receiver = telephone.LinkedTelephones.FirstOrDefault().Owner;

            if (receiver == default ||
                !TryComp<HolopadComponent>(receiver, out var receiverHolo) ||
                receiverHolo.Hologram == null)
                continue;

            var hologram = receiverHolo.Hologram.Value.Owner;

            // Первый раз под этого пользователя — замораживаем.
            if (transmitter.ViewerBody != viewer)
            {
                if (transmitter.ViewerBody is { } old && Exists(old))
                    Unfreeze(old);

                transmitter.ViewerBody = viewer;
                EnsureComp<RemoteViewerFrozenComponent>(viewer);
                _blocker.UpdateCanMove(viewer);
                Dirty(uid, transmitter);
            }

            // Наводим глаз на голограмму.
            if (transmitter.RemoteHologram != hologram)
            {
                if (TryComp<EyeComponent>(viewer, out var eye))
                    _eye.SetTarget(viewer, hologram, eye);

                transmitter.RemoteHologram = hologram;
                transmitter.ReceiverHolopad = receiver;
                Dirty(uid, transmitter);
            }
        }
    }

    private void Cleanup(Entity<RemoteHolopadTransmitterComponent> ent)
    {
        if (ent.Comp.ViewerBody is { } viewer && Exists(viewer))
        {
            if (TryComp<EyeComponent>(viewer, out var eye))
                _eye.SetTarget(viewer, null, eye);

            Unfreeze(viewer);
        }

        if (ent.Comp.ViewerBody == null &&
            ent.Comp.RemoteHologram == null &&
            ent.Comp.ReceiverHolopad == null)
            return;

        ent.Comp.ViewerBody = null;
        ent.Comp.RemoteHologram = null;
        ent.Comp.ReceiverHolopad = null;
        Dirty(ent);
    }

    private void Unfreeze(EntityUid viewer)
    {
        RemComp<RemoteViewerFrozenComponent>(viewer);
        _blocker.UpdateCanMove(viewer);
    }

    // ---------- Кнопка ----------

    private void OnStopBroadcast(Entity<RemoteHolopadTransmitterComponent> ent,
                                 ref RemoteHolopadStopBroadcastMessage args)
    {
        if (!TryComp<TelephoneComponent>(ent, out var tel))
            return;

        _telephone.EndTelephoneCalls((ent.Owner, tel));
        // Cleanup произойдёт в Update, когда состояние выйдет из InCall.
    }

    // ---------- Аварийные выходы ----------

    private void OnTransmitterShutdown(Entity<RemoteHolopadTransmitterComponent> ent,
                                       ref ComponentShutdown args)
    {
        Cleanup(ent);
    }

    private void OnTransmitterPower(Entity<RemoteHolopadTransmitterComponent> ent,
                                    ref PowerChangedEvent args)
    {
        if (!args.Powered && TryComp<TelephoneComponent>(ent, out var tel))
            _telephone.EndTelephoneCalls((ent.Owner, tel));
    }

    // ---------- Заморозка ----------

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

    // ---------- Прерывания ----------

    private void OnViewerDamaged(Entity<RemoteViewerFrozenComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        Interrupt(ent.Owner);
    }

    private void OnViewerPulled(Entity<RemoteViewerFrozenComponent> ent, ref PullAttemptEvent args)
    {
        Interrupt(ent.Owner);
    }

    private void OnViewerMobState(Entity<RemoteViewerFrozenComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        Interrupt(ent.Owner);
    }

    private void Interrupt(EntityUid viewer)
    {
        var query = EntityQueryEnumerator<RemoteHolopadTransmitterComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ViewerBody != viewer)
                continue;

            if (TryComp<TelephoneComponent>(uid, out var tel))
                _telephone.EndTelephoneCalls((uid, tel));
            break;
        }
    }
}