using Content.Server.DeviceLinking.Systems;
using Content.Shared._Fish.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Access;

public sealed class IdCardReaderSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly DeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private EntityQuery<DeviceLinkSourceComponent> _sourceQuery;

    public override void Initialize()
    {
        base.Initialize();

        _sourceQuery = GetEntityQuery<DeviceLinkSourceComponent>();

        SubscribeLocalEvent<IdCardReaderComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<IdCardReaderComponent, EntInsertedIntoContainerMessage>(OnCardInserted);
        SubscribeLocalEvent<IdCardReaderComponent, EntRemovedFromContainerMessage>(OnCardRemoved);
        SubscribeLocalEvent<IdCardReaderComponent, AccessReaderConfigurationChangedEvent>(OnAccessReaderChanged);
        SubscribeLocalEvent<IdCardReaderComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<IdCardReaderComponent, PortDisconnectedEvent>(OnPortDisconnected);
    }

    private void OnStartup(Entity<IdCardReaderComponent> ent, ref ComponentStartup args)
    {
        TryScheduleRefresh(ent);
    }

    private void OnCardInserted(Entity<IdCardReaderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != IdCardReaderComponent.CardSlotId)
            return;

        TryReadCard(ent, args.Entity);
    }

    private void OnCardRemoved(Entity<IdCardReaderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        TryReportCardRemoved(ent, args.Container.ID);
    }

    private void OnAccessReaderChanged(
        Entity<IdCardReaderComponent> ent,
        ref AccessReaderConfigurationChangedEvent args)
    {
        TryScheduleRefresh(ent);
    }

    private void OnSignalReceived(Entity<IdCardReaderComponent> ent, ref SignalReceivedEvent args)
    {
        TryReceiveSignal(ent, args.Port, args.Trigger, args.Data);
    }

    private void OnPortDisconnected(Entity<IdCardReaderComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Port != ent.Comp.AuthorizationPort && args.Port != ent.Comp.DoorStatusPort)
            return;

        TryScheduleRefresh(ent);
    }

    /// <summary>
    /// Пытается считать ID-карту и обновить итоговую авторизацию.
    /// </summary>
    public bool TryReadCard(Entity<IdCardReaderComponent> ent, EntityUid card)
    {
        if (!CanReadCard(ent, card, out var accessReader))
            return false;

        DoReadCard(ent, card, accessReader);
        return true;
    }

    /// <summary>
    /// Проверяет, может ли сущность быть считана как ID-карта.
    /// </summary>
    public bool CanReadCard(
        Entity<IdCardReaderComponent> ent,
        EntityUid card,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AccessReaderComponent? accessReader)
    {
        accessReader = null;

        if (!HasComp<IdCardComponent>(card))
            return false;

        return TryComp(ent, out accessReader);
    }

    /// <summary>
    /// Пытается сообщить об извлечении ID-карты из нужного слота.
    /// </summary>
    public bool TryReportCardRemoved(Entity<IdCardReaderComponent> ent, string slotId)
    {
        if (!CanReportCardRemoved(slotId))
            return false;

        DoReportCardRemoved(ent);
        return true;
    }

    /// <summary>
    /// Проверяет, относится ли извлечение к слоту считывателя ID-карт.
    /// </summary>
    public bool CanReportCardRemoved(string slotId)
    {
        return slotId == IdCardReaderComponent.CardSlotId;
    }

    /// <summary>
    /// Пытается применить устойчивый сигнал другого считывателя или затвора.
    /// </summary>
    public bool TryReceiveSignal(
        Entity<IdCardReaderComponent> ent,
        string port,
        EntityUid? source,
        NetworkPayload? data)
    {
        if (!CanReceiveSignal(ent, port, source, data, out var state))
            return false;

        DoReceiveSignal(ent, port, source!.Value, state);
        return true;
    }

    /// <summary>
    /// Проверяет, является ли входной сигнал пригодным устойчивым состоянием.
    /// </summary>
    public bool CanReceiveSignal(
        Entity<IdCardReaderComponent> ent,
        string port,
        EntityUid? source,
        NetworkPayload? data,
        out SignalState state)
    {
        state = SignalState.Momentary;

        if (port != ent.Comp.AuthorizationPort && port != ent.Comp.DoorStatusPort)
            return false;

        if (source == null || source == ent.Owner || data == null)
            return false;

        return data.TryGetValue(DeviceNetworkConstants.LogicState, out state) && state != SignalState.Momentary;
    }

    /// <summary>
    /// Пытается запланировать перечитывание карты и подключённых источников.
    /// </summary>
    public bool TryScheduleRefresh(Entity<IdCardReaderComponent> ent)
    {
        if (!CanScheduleRefresh(ent))
            return false;

        DoScheduleRefresh(ent);
        return true;
    }

    /// <summary>
    /// Проверяет, можно ли запланировать перечитывание состояния.
    /// </summary>
    public bool CanScheduleRefresh(Entity<IdCardReaderComponent> ent)
    {
        return !Deleted(ent);
    }

    /// <summary>
    /// Пытается перечитать карту и устойчивые состояния подключённых источников.
    /// </summary>
    public bool TryRefreshState(Entity<IdCardReaderComponent> ent)
    {
        if (!CanRefreshState(ent))
            return false;

        DoRefreshState(ent);
        return true;
    }

    /// <summary>
    /// Проверяет наличие компонентов, необходимых для перечитывания состояния.
    /// </summary>
    public bool CanRefreshState(Entity<IdCardReaderComponent> ent)
    {
        return TryComp<DeviceLinkSinkComponent>(ent, out _);
    }

    /// <summary>
    /// Пытается отправить очередную команду закрытия.
    /// </summary>
    public bool TryRequestClose(
        Entity<IdCardReaderComponent> ent,
        ActiveIdCardReaderComponent active)
    {
        if (!CanRequestClose(active))
            return false;

        DoRequestClose(ent, active);
        return true;
    }

    /// <summary>
    /// Проверяет, наступило ли время очередной команды закрытия.
    /// </summary>
    public bool CanRequestClose(ActiveIdCardReaderComponent active)
    {
        return active.NextCloseAttempt != null && active.NextCloseAttempt <= _timing.CurTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveIdCardReaderComponent, IdCardReaderComponent>();
        while (query.MoveNext(out var uid, out var active, out var reader))
        {
            if (active.RefreshState)
            {
                active.RefreshState = false;
                TryRefreshState((uid, reader));
            }

            TryRequestClose((uid, reader), active);
            TryFinishProcessing(uid, active);
        }
    }

    private void DoReadCard(
        Entity<IdCardReaderComponent> ent,
        EntityUid card,
        AccessReaderComponent accessReader)
    {
        _deviceLink.InvokePort(ent, ent.Comp.CardInsertedPort);
        SetLocalAuthorization(ent, _accessReader.IsAllowed(card, ent, accessReader));
        UpdateAuthorization(ent);
    }

    private void DoReportCardRemoved(Entity<IdCardReaderComponent> ent)
    {
        _deviceLink.InvokePort(ent, ent.Comp.CardRemovedPort);
        SetLocalAuthorization(ent, false);
        UpdateAuthorization(ent);
    }

    private void DoReceiveSignal(
        Entity<IdCardReaderComponent> ent,
        string port,
        EntityUid source,
        SignalState state)
    {
        var sources = port == ent.Comp.AuthorizationPort
            ? ent.Comp.RemoteAuthorizations
            : ent.Comp.OpenDoors;
        var changed = state == SignalState.High
            ? sources.Add(source)
            : sources.Remove(source);

        if (!changed)
            return;

        if (port == ent.Comp.AuthorizationPort)
        {
            UpdateAuthorization(ent);
            return;
        }

        if (ent.Comp.OpenDoors.Count > 0)
            ScheduleClose(ent, restart: false);
        else
            CancelClosing(ent);
    }

    private void DoScheduleRefresh(Entity<IdCardReaderComponent> ent)
    {
        EnsureComp<ActiveIdCardReaderComponent>(ent).RefreshState = true;
    }

    private void DoRefreshState(Entity<IdCardReaderComponent> ent)
    {
        var hadOpenDoors = ent.Comp.OpenDoors.Count > 0;
        ent.Comp.RemoteAuthorizations.Clear();
        ent.Comp.OpenDoors.Clear();

        RefreshLocalAuthorization(ent);
        RefreshLinkedStates(ent);
        UpdateAuthorization(ent);

        if (ent.Comp.OpenDoors.Count > 0)
            ScheduleClose(ent, restart: false);
        else if (hadOpenDoors)
            CancelClosing(ent);
    }

    private void DoRequestClose(
        Entity<IdCardReaderComponent> ent,
        ActiveIdCardReaderComponent active)
    {
        _deviceLink.InvokePort(ent, ent.Comp.CloseRequestPort);

        if (ent.Comp.OpenDoors.Count == 0)
        {
            active.NextCloseAttempt = null;
            return;
        }

        var retryDelay = ent.Comp.CloseRetryDelay > TimeSpan.Zero
            ? ent.Comp.CloseRetryDelay
            : TimeSpan.FromSeconds(1);
        active.NextCloseAttempt = _timing.CurTime + retryDelay;
    }

    private void RefreshLocalAuthorization(Entity<IdCardReaderComponent> ent)
    {
        var authorized = false;

        if (_itemSlots.TryGetSlot(ent, IdCardReaderComponent.CardSlotId, out var slot) &&
            slot.Item is { } card &&
            CanReadCard(ent, card, out var accessReader))
        {
            authorized = _accessReader.IsAllowed(card, ent, accessReader);
        }

        SetLocalAuthorization(ent, authorized);
    }

    private void SetLocalAuthorization(Entity<IdCardReaderComponent> ent, bool authorized)
    {
        ent.Comp.LocalAuthorization = authorized;
        _deviceLink.SendSignal(ent, ent.Comp.LocalAuthorizationPort, authorized);
    }

    private void RefreshLinkedStates(Entity<IdCardReaderComponent> ent)
    {
        if (!TryComp<DeviceLinkSinkComponent>(ent, out var sink))
            return;

        foreach (var sourceUid in sink.LinkedSources)
        {
            if (sourceUid == ent.Owner || !_sourceQuery.TryComp(sourceUid, out var source))
                continue;

            if (_deviceLink.IsSendingHighTo((sourceUid, source), ent, ent.Comp.AuthorizationPort))
                ent.Comp.RemoteAuthorizations.Add(sourceUid);

            if (_deviceLink.IsSendingHighTo((sourceUid, source), ent, ent.Comp.DoorStatusPort))
                ent.Comp.OpenDoors.Add(sourceUid);
        }
    }

    private void UpdateAuthorization(Entity<IdCardReaderComponent> ent)
    {
        var authorizationCount = ent.Comp.RemoteAuthorizations.Count;
        if (ent.Comp.LocalAuthorization)
            authorizationCount++;

        var authorized = authorizationCount >= Math.Max(1, ent.Comp.RequiredAuthorizations);
        if (authorized == ent.Comp.AuthorizationOutput)
            return;

        ent.Comp.AuthorizationOutput = authorized;
        _deviceLink.SendSignal(ent, ent.Comp.AccessGrantedPort, authorized);

        if (authorized)
            ScheduleClose(ent, restart: true);
    }

    private void ScheduleClose(Entity<IdCardReaderComponent> ent, bool restart)
    {
        var active = EnsureComp<ActiveIdCardReaderComponent>(ent);
        if (!restart && active.NextCloseAttempt != null)
            return;

        var closeDelay = ent.Comp.CloseDelay > TimeSpan.Zero
            ? ent.Comp.CloseDelay
            : TimeSpan.Zero;
        active.NextCloseAttempt = _timing.CurTime + closeDelay;
    }

    private void CancelClosing(Entity<IdCardReaderComponent> ent)
    {
        if (!TryComp<ActiveIdCardReaderComponent>(ent, out var active))
            return;

        active.NextCloseAttempt = null;
        TryFinishProcessing(ent, active);
    }

    private void TryFinishProcessing(EntityUid uid, ActiveIdCardReaderComponent active)
    {
        if (active.RefreshState || active.NextCloseAttempt != null)
            return;

        RemCompDeferred<ActiveIdCardReaderComponent>(uid);
    }
}
