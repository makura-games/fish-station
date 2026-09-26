using Content.Server.Medical;
using Content.Server.Medical.Components;
using Content.Server.Store.Systems;
using Content.Shared._Fish.PAI;
using Content.Shared._Sunrise.CriminalRecords;
using Content.Shared._Sunrise.CriminalRecords.Components;
using Content.Shared._Sunrise.InnateItem;
using Content.Shared.Actions;
using Content.Shared.Doors.Components;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.MedicalScanner;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Content.Shared.StationRecords;
using Content.Shared.Store;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared.Damage.Systems;

namespace Content.Server._Fish.PAI;

public sealed partial class SyndicatePaiSystem : SharedSyndicatePaiSystem
{
    [Dependency] private SharedPopupSystem _serverPopup = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedActionsSystem _serverActions = default!;
    [Dependency] private SharedContainerSystem _serverContainer = default!;
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private HealthAnalyzerSystem _healthAnalyzer = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private SharedUserInterfaceSystem _serverUi = default!;
    [Dependency] private SharedStationSystem _station = default!;

    private const string SunriseCriminalRecordsBui = "SunriseCriminalRecordsConsoleBoundUserInterface";

    private static readonly EntProtoId InnateInstantActionProto = "InnateInstantActionAction";
    private static readonly EntProtoId InnateEntityTargetActionProto = "InnateEntityTargetAction";

    private static readonly HashSet<string> ModuleListingIds =
    [
        "SyndicatePaiMedical",
        "SyndicatePaiAutoDispenser",
        "SyndicatePaiDoorHack",
        "SyndicatePaiCrewMonitor",
        "SyndicatePaiCameras",
        "SyndicatePaiSecRecords",
        "SyndicatePaiDisguise",
        "SyndicatePaiAtmosSensor",
        "SyndicatePaiMassScanner",
        "SyndicatePaiMidi",
        "SyndicatePaiStationMap",
    ];

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<SyndicatePaiComponent>(SyndicatePaiUiKey.Key,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnUiOpened);
                subs.Event<SyndicatePaiInjectCarrierMessage>(OnInjectMessage);
                subs.Event<SyndicatePaiSelectReagentMessage>(OnSelectMessage);
                subs.Event<SyndicatePaiSetTransferAmountMessage>(OnSetTransferAmount);
                subs.Event<SyndicatePaiSetAutoEnabledMessage>(OnSetAutoEnabled);
                subs.Event<SyndicatePaiSetAutoThresholdMessage>(OnSetAutoThreshold);
                subs.Event<SyndicatePaiImprintMasterMessage>(OnImprintMessage);
            });

        SubscribeLocalEvent<SyndicatePaiComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<SyndicatePaiComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<SyndicatePaiComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<SyndicatePaiComponent, SyndicatePaiScanOwnerEvent>(OnScanOwner);
        SubscribeLocalEvent<SyndicatePaiComponent, SyndicatePaiDoorHackEvent>(OnDoorHack);
        SubscribeLocalEvent<SyndicatePaiComponent, SyndicatePaiOpenSecRecordsEvent>(OnOpenSecRecords);
        SubscribeLocalEvent<StoreBuyFinishedEvent>(OnStoreBuyFinished);
    }

    private void OnUiOpened(Entity<SyndicatePaiComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUiState(ent);
    }

    private void OnInjectMessage(Entity<SyndicatePaiComponent> ent, ref SyndicatePaiInjectCarrierMessage args)
    {
        // Только владелец (мастер); без клика по спрайту
        TryInjectOwner(ent, args.Actor);
    }

    private void OnSetTransferAmount(Entity<SyndicatePaiComponent> ent, ref SyndicatePaiSetTransferAmountMessage args)
    {
        TrySetTransferAmount(ent, args.Actor, FixedPoint2.New(args.Amount));
    }

    private void OnImprintMessage(Entity<SyndicatePaiComponent> ent, ref SyndicatePaiImprintMasterMessage args)
    {
        if (!TryGetCarrier(ent.Owner, out var carrier) || carrier == null)
        {
            _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-no-carrier"), ent.Owner, args.Actor);
            return;
        }

        // Импринт только носителем или самим пИИ
        if (args.Actor != carrier && args.Actor != ent.Owner)
        {
            _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-imprint-denied"), ent.Owner, args.Actor);
            return;
        }

        TryImprintMaster(ent, carrier.Value, args.Actor);
    }

    private void OnGetVerbs(Entity<SyndicatePaiComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        // Импринт только текущему носителю, не любому прохожему
        if (TryGetCarrier(ent.Owner, out var carrier) && carrier == user && user != ent.Comp.Master)
        {
            AlternativeVerb imprint = new()
            {
                Text = Loc.GetString("syndicate-pai-verb-imprint"),
                Act = () => TryImprintMaster(ent, user, user),
                Priority = 2,
            };
            args.Verbs.Add(imprint);
        }
    }

    private void OnMindRemoved(Entity<SyndicatePaiComponent> ent, ref MindRemovedMessage args)
    {
        ent.Comp.Master = null;
        Dirty(ent);
    }

    private void OnUseInHand(Entity<SyndicatePaiComponent> ent, ref UseInHandEvent args)
    {
        if (ent.Comp.Master != null)
            return;

        if (args.User == ent.Owner)
            return;

        TryImprintMaster(ent, args.User, args.User);
    }

    private void OnScanOwner(Entity<SyndicatePaiComponent> ent, ref SyndicatePaiScanOwnerEvent args)
    {
        if (args.Handled)
            return;

        // Handled только при успехе — иначе useDelay сработает впустую
        if (!ent.Comp.MedicalUnlocked)
        {
            _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-module-locked"), ent.Owner, args.Performer);
            return;
        }

        if (!TryGetOwnerTarget(ent, out var owner) || owner == null)
        {
            _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-no-owner"), ent.Owner, args.Performer);
            return;
        }

        if (!IsHeldByOwner(ent, owner.Value))
        {
            _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-not-in-owner-inventory"), ent.Owner, args.Performer);
            return;
        }

        if (!TryGetAnalyzer(ent, out var analyzer) || analyzer == null)
        {
            _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-no-analyzer"), ent.Owner, args.Performer);
            return;
        }

        if (!TryComp<HealthAnalyzerComponent>(analyzer.Value, out var analyzerComp))
            return;

        // Моментальный скан владельца без do-after
        analyzerComp.ScannedEntity = owner.Value;
        _itemToggle.TryActivate(analyzer.Value);
        _serverUi.OpenUi(analyzer.Value, HealthAnalyzerUiKey.Key, args.Performer);
        _healthAnalyzer.UpdateScannedUser(analyzer.Value, owner.Value, true);
        args.Handled = true;
    }

    private void OnDoorHack(Entity<SyndicatePaiComponent> ent, ref SyndicatePaiDoorHackEvent args)
    {
        if (args.Handled)
            return;

        // Handled=false на всех отказах — иначе useDelay/заряды сработают без цели
        if (!ent.Comp.DoorHackUnlocked)
        {
            _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-module-locked"), ent.Owner, args.Performer);
            return;
        }

        if (!TryGetAccessBreaker(ent, out var breaker) || breaker == null)
        {
            _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-door-hack-missing"), ent.Owner, args.Performer);
            return;
        }

        if (!TryGetOwnerTarget(ent, out var owner) || owner == null)
        {
            _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-no-owner"), ent.Owner, args.Performer);
            return;
        }

        var origin = _transform.GetMapCoordinates(owner.Value);
        var doors = _lookup.GetEntitiesInRange<DoorComponent>(origin, ent.Comp.DoorHackRadius);
        var hacked = 0;

        foreach (var door in doors)
        {
            if (_emag.TryEmagEffect(breaker.Value, args.Performer, door))
                hacked++;
        }

        if (hacked <= 0)
        {
            _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-door-hack-none"), ent.Owner, args.Performer);
            return;
        }

        args.Handled = true;
        _serverPopup.PopupEntity(
            Loc.GetString("syndicate-pai-door-hack-success", ("count", hacked)),
            ent.Owner,
            args.Performer);
    }

    private void OnOpenSecRecords(Entity<SyndicatePaiComponent> ent, ref SyndicatePaiOpenSecRecordsEvent args)
    {
        if (args.Handled)
            return;

        if (!ent.Comp.SecRecordsUnlocked)
        {
            _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-module-locked"), ent.Owner, args.Performer);
            return;
        }

        // Консоль на пИИ в инвентаре не имеет GridUid — привязываем станцию носителя/игрока
        if (!TryBindSecRecordsStation(ent, args.Performer))
        {
            _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-sec-no-station"), ent.Owner, args.Performer);
            return;
        }

        EnsureSecRecordsUi(ent.Owner);
        // Убираем старую ванильную консоль, если осталась после прошлых версий
        RemComp<Content.Shared.CriminalRecords.Components.CriminalRecordsConsoleComponent>(ent.Owner);
        EnsureComp<SunriseCriminalRecordsConsoleComponent>(ent.Owner);
        _serverUi.TryToggleUi(ent.Owner, SunriseCriminalRecordsConsoleKey.Key, args.Performer);
        args.Handled = true;
    }

    /// <summary>
    /// Регистрирует Sunrise BUI на пИИ без правки ванильного прототипа.
    /// </summary>
    private void EnsureSecRecordsUi(EntityUid pai)
    {
        // interactionRange <= 0 — без лимита; пИИ открывает UI на себе из инвентаря/КПК
        _serverUi.SetUi(pai, SunriseCriminalRecordsConsoleKey.Key,
            new InterfaceData(SunriseCriminalRecordsBui, interactionRange: -1f, requireInputValidation: false));
    }

    /// <summary>
    /// Fish использует Sunrise criminal records; ванильный GetOwningStation для предметов в контейнере пустой.
    /// </summary>
    private bool TryBindSecRecordsStation(Entity<SyndicatePaiComponent> ent, EntityUid user)
    {
        EntityUid? station = FindStationNear(ent.Owner);

        if (TryGetCarrier(ent.Owner, out var carrier) && carrier != null)
            station ??= FindStationNear(carrier.Value);

        station ??= FindStationNear(user);

        if (ent.Comp.Master is { Valid: true } master && !TerminatingOrDeleted(master))
            station ??= FindStationNear(master);

        if (station == null || !HasComp<StationRecordsComponent>(station))
        {
            foreach (var candidate in _station.GetStations())
            {
                if (!HasComp<StationRecordsComponent>(candidate))
                    continue;

                station = candidate;
                break;
            }
        }

        if (station == null || !HasComp<StationRecordsComponent>(station))
            return false;

        EnsureComp<StationTrackerComponent>(ent.Owner);
        _station.SetStation(ent.Owner, station);
        return true;
    }

    private EntityUid? FindStationNear(EntityUid entity)
    {
        if (HasComp<StationDataComponent>(entity))
            return entity;

        if (TryComp<StationMemberComponent>(entity, out var member))
            return member.Station;

        var current = entity;
        for (var depth = 0; depth < 24; depth++)
        {
            if (!TryComp(current, out TransformComponent? xform))
                break;

            if (xform.GridUid is { Valid: true } grid)
            {
                if (TryComp<StationMemberComponent>(grid, out var gridMember))
                    return gridMember.Station;
            }

            if (HasComp<MapGridComponent>(current) &&
                TryComp<StationMemberComponent>(current, out var selfMember))
            {
                return selfMember.Station;
            }

            var parent = xform.ParentUid;
            if (!parent.IsValid() || parent == current)
                break;

            current = parent;
        }

        return null;
    }

    private void OnStoreBuyFinished(ref StoreBuyFinishedEvent args)
    {
        if (!TryComp<SyndicatePaiComponent>(args.StoreUid, out var pai))
            return;

        var listingId = args.PurchasedItem.ID;
        if (!ModuleListingIds.Contains(listingId))
            return;

        Entity<SyndicatePaiComponent> ent = (args.StoreUid, pai);
        switch (listingId)
        {
            case "SyndicatePaiMedical":
                UnlockMedical(ent);
                break;
            case "SyndicatePaiAutoDispenser":
                UnlockAutoDispenser(ent);
                break;
            case "SyndicatePaiDoorHack":
                UnlockDoorHack(ent);
                break;
            case "SyndicatePaiCrewMonitor":
                GrantInnateTool(ent, "HandheldEmergencyCrewMonitorBorg", entityTarget: false, grantAction: true);
                _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-module-crew-monitor-unlocked"), ent.Owner, ent.Owner);
                break;
            case "SyndicatePaiCameras":
                GrantInnateTool(ent, "PortableSurveillanceCameraMonitorUnpowered", entityTarget: false, grantAction: true);
                _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-module-cameras-unlocked"), ent.Owner, ent.Owner);
                break;
            case "SyndicatePaiSecRecords":
                UnlockSecRecords(ent);
                break;
            case "SyndicatePaiDisguise":
                UnlockDisguise(ent);
                break;
            case "SyndicatePaiAtmosSensor":
                GrantInnateTool(ent, "GasAnalyzerPai", entityTarget: false, grantAction: true);
                break;
            // Programs use productAction in catalog — nothing else here
        }
    }

    private void UnlockMedical(Entity<SyndicatePaiComponent> ent)
    {
        if (ent.Comp.MedicalUnlocked)
            return;

        ent.Comp.MedicalUnlocked = true;
        // Инструменты только во внутреннем контейнере — без innate-кликов по чужим
        GrantInnateTool(ent, ent.Comp.HypoPrototype, entityTarget: true, grantAction: false);
        GrantInnateTool(ent, ent.Comp.AnalyzerPrototype, entityTarget: true, grantAction: false);

        if (TryGetManualHypo(ent, out var manualHypo) && manualHypo != null)
            ClearHypoReservoir(manualHypo.Value);

        // OpenMedical выдаётся через productAction листинга; ScanOwner — отдельно
        _serverActions.AddAction(ent.Owner, ref ent.Comp.ScanOwnerActionEntity, ent.Comp.ScanOwnerAction);
        Dirty(ent);
        _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-module-medical-unlocked"), ent.Owner, ent.Owner);
    }

    private void UnlockDoorHack(Entity<SyndicatePaiComponent> ent)
    {
        if (ent.Comp.DoorHackUnlocked)
            return;

        ent.Comp.DoorHackUnlocked = true;
        // Unlimited AccessBreaker — существующая система взлома доступа
        GrantInnateTool(ent, "AccessBreakerUnlimited", entityTarget: true, grantAction: false);
        Dirty(ent);
        _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-module-door-unlocked"), ent.Owner, ent.Owner);
    }

    private void UnlockSecRecords(Entity<SyndicatePaiComponent> ent)
    {
        if (ent.Comp.SecRecordsUnlocked)
            return;

        ent.Comp.SecRecordsUnlocked = true;
        // Sunrise criminal records — тот же UI, что у станционных/handheld консолей Fish
        EnsureSecRecordsUi(ent.Owner);
        EnsureComp<SunriseCriminalRecordsConsoleComponent>(ent.Owner);
        Dirty(ent);
        _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-module-sec-unlocked"), ent.Owner, ent.Owner);
    }

    private void UnlockDisguise(Entity<SyndicatePaiComponent> ent)
    {
        if (ent.Comp.Disguised)
            return;

        // Только спрайт (клиент); имя/описание пИИ не меняем
        ent.Comp.Disguised = true;
        Dirty(ent);

        _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-module-disguise-unlocked"), ent.Owner, ent.Owner);
    }

    private bool TryGetAccessBreaker(Entity<SyndicatePaiComponent> ent, out EntityUid? breaker)
    {
        breaker = null;
        if (!_serverContainer.TryGetContainer(ent.Owner, SyndicatePaiComponent.InnateItemContainerId, out var container))
            return false;

        foreach (var contained in container.ContainedEntities)
        {
            if (!TryComp<EmagComponent>(contained, out var emag))
                continue;

            if (!_emag.CompareFlag(emag.EmagType, EmagType.Access))
                continue;

            breaker = contained;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Выдаёт инструмент в innate_items и создаёт действие (как InnateItemSystem).
    /// </summary>
    private void GrantInnateTool(Entity<SyndicatePaiComponent> ent, EntProtoId proto, bool entityTarget, bool grantAction)
    {
        var innate = EnsureComp<InnateItemComponent>(ent.Owner);
        if (!_serverContainer.TryGetContainer(ent.Owner, SyndicatePaiComponent.InnateItemContainerId, out var container))
        {
            var manager = EnsureComp<ContainerManagerComponent>(ent.Owner);
            container = _serverContainer.EnsureContainer<Container>(ent.Owner, SyndicatePaiComponent.InnateItemContainerId, manager);
        }

        // Не дублируем уже выданный прототип
        foreach (var existing in container.ContainedEntities)
        {
            if (MetaData(existing).EntityPrototype?.ID == proto.Id)
                return;
        }

        var spawned = Spawn(proto);
        if (TryComp<Content.Shared.UserInterface.ActivatableUIComponent>(spawned, out var activatableUi))
        {
            activatableUi.RequiresComplex = false;
            activatableUi.InHandsOnly = false;
            activatableUi.RequireActiveHand = false;
            Dirty(spawned, activatableUi);
        }

        _serverContainer.Insert(spawned, container);

        if (!grantAction)
            return;

        var actionProto = entityTarget ? InnateEntityTargetActionProto : InnateInstantActionProto;
        var action = Spawn(actionProto);

        _serverActions.SetIcon(action, new SpriteSpecifier.EntityPrototype(proto));
        if (entityTarget)
            _serverActions.SetEvent(action, new InnateEntityTargetActionEvent(spawned));
        else
            _serverActions.SetEvent(action, new InnateInstantActionEvent(spawned));

        _metadata.SetEntityName(action, MetaData(spawned).EntityName);
        _metadata.SetEntityDescription(action, MetaData(spawned).EntityDescription);
        _actionContainer.AddAction(ent.Owner, action);
        _serverActions.AddAction(ent.Owner, action, ent.Owner);
        innate.Actions.Add(action);
        Dirty(ent.Owner, innate);
    }
}
