using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Cargo.Components;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared._Fish.CentCommSupply;
using Content.Shared.Access.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Labels.Components;
using Content.Shared.Paper;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Content.Shared.Storage.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Fish.CentCommSupply;

/// <summary>
/// Handles CentComm supply console validation, packing and delivery.
/// </summary>
public sealed partial class CentCommSupplyConsoleSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private EntityStorageSystem _storage = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private readonly Dictionary<Guid, TimeSpan> _recentOrders = new();
    private readonly HashSet<Guid> _acceptedOrders = new();
    private readonly HashSet<EntityUid> _staticIntersecting = new();
    private readonly List<PendingOrder> _pendingOrders = new();
    private readonly List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent Transform)> _pads = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<CentCommSupplyConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CentCommSupplyConsoleComponent, CentCommSupplySelectStationMessage>(OnSelectStation);
        SubscribeLocalEvent<CentCommSupplyConsoleComponent, CentCommSupplyOrderMessage>(OnOrder);
    }

    /// <summary>
    /// Clears pending and recently accepted orders when the round restarts.
    /// </summary>
    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        foreach (var order in _pendingOrders)
            RollbackCrates(order.Crates);

        _pendingOrders.Clear();
        _recentOrders.Clear();
        _acceptedOrders.Clear();
    }

    /// <summary>
    /// Sends the current console state when the UI is opened.
    /// </summary>
    private void OnUiOpened(Entity<CentCommSupplyConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    /// <summary>
    /// Routes station selection requests to the public selection API.
    /// </summary>
    private void OnSelectStation(Entity<CentCommSupplyConsoleComponent> ent, ref CentCommSupplySelectStationMessage args)
    {
        TrySelectStation(ent, args.Actor, args.Station, args.SelectionId);
    }

    /// <summary>
    /// Routes order requests to the public order API.
    /// </summary>
    private void OnOrder(Entity<CentCommSupplyConsoleComponent> ent, ref CentCommSupplyOrderMessage args)
    {
        TryPlaceOrder(ent, args.Actor, args.OrderId, args.Station, args.Entries);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingOrders.Count == 0)
            return;

        var now = _timing.CurTime;
        for (var i = _pendingOrders.Count - 1; i >= 0; i--)
        {
            var order = _pendingOrders[i];
            if (now < order.DeliverAt)
                continue;

            if (!TryComp<StationDataComponent>(order.Station, out _))
            {
                RollbackCrates(order.Crates);
                _pendingOrders.RemoveAt(i);
                continue;
            }

            var freePallets = GetFreeBuyPallets(order.Station);
            if (freePallets.Count < order.Crates.Count)
            {
                if (now >= order.ExpireAt)
                {
                    RollbackCrates(order.Crates);
                    _pendingOrders.RemoveAt(i);
                    NotifyDeliveryTimeout(order);
                    continue;
                }

                order.DeliverAt = now + TimeSpan.FromSeconds(5);
                continue;
            }

            if (!TryDeliver(order, freePallets))
            {
                RollbackCrates(order.Crates);
                _pendingOrders.RemoveAt(i);
                Log.Error($"Failed to deliver CentComm supply order #{order.OrderNumber} to {ToPrettyString(order.Station)}.");
                continue;
            }

            _pendingOrders.RemoveAt(i);
        }
    }

    /// <summary>
    /// Attempts to select an orderable station and records the request identifier.
    /// </summary>
    public bool TrySelectStation(
        Entity<CentCommSupplyConsoleComponent> ent,
        EntityUid actor,
        NetEntity stationNet,
        Guid selectionId)
    {
        ent.Comp.LastStationSelectionId = selectionId;

        if (!CanSelectStation(ent, actor, stationNet, out var error))
        {
            if (error != null)
                Fail(ent, actor, error);

            return false;
        }

        DoSelectStation(ent, GetEntity(stationNet), selectionId);
        return true;
    }

    /// <summary>
    /// Checks whether the actor may select the requested station.
    /// </summary>
    public bool CanSelectStation(
        Entity<CentCommSupplyConsoleComponent> ent,
        EntityUid actor,
        NetEntity stationNet,
        out string? error)
    {
        if (!_access.IsAllowed(actor, ent.Owner))
        {
            error = Loc.GetString("centcomm-supply-error-access");
            return false;
        }

        var station = GetEntity(stationNet);
        var owningStation = _station.GetOwningStation(ent.Owner);
        if (station == owningStation
            || !TryComp<StationDataComponent>(station, out var data)
            || !HasBuyPallets(data)
            || !TryComp<StationCargoOrderDatabaseComponent>(station, out _))
        {
            error = Loc.GetString("centcomm-supply-error-invalid-station");
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Applies a validated station selection and updates the UI.
    /// </summary>
    private void DoSelectStation(
        Entity<CentCommSupplyConsoleComponent> ent,
        EntityUid station,
        Guid selectionId)
    {
        ent.Comp.SelectedStation = station;
        ent.Comp.LastStationSelectionId = selectionId;
        UpdateUi(ent);
    }

    /// <summary>
    /// Attempts to validate, pack and queue an order.
    /// </summary>
    public bool TryPlaceOrder(
        Entity<CentCommSupplyConsoleComponent> ent,
        EntityUid actor,
        Guid orderId,
        NetEntity stationNet,
        List<CentCommSupplyOrderEntry>? entries)
    {
        if (!CanPlaceOrder(ent, actor, orderId, stationNet, entries, out var plan, out var error))
        {
            if (error != null)
                Fail(ent, actor, error);

            return false;
        }

        if (!TryDoPlaceOrder(ent, actor, orderId, plan!, out error))
        {
            if (error != null)
                Fail(ent, actor, error);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates an order without changing console or order state.
    /// </summary>
    private bool CanPlaceOrder(
        Entity<CentCommSupplyConsoleComponent> ent,
        EntityUid actor,
        Guid orderId,
        NetEntity stationNet,
        List<CentCommSupplyOrderEntry>? entries,
        out OrderPlan? plan,
        out string? error)
    {
        plan = null;
        error = null;

        if (!_access.IsAllowed(actor, ent.Owner))
        {
            error = Loc.GetString("centcomm-supply-error-access");
            return false;
        }

        if (orderId == Guid.Empty
            || _recentOrders.ContainsKey(orderId)
            || _acceptedOrders.Contains(orderId))
        {
            return false;
        }

        var now = _timing.CurTime;
        if (now < ent.Comp.NextOrderTime)
        {
            var secondsCount = Math.Max(1, (int) Math.Ceiling((ent.Comp.NextOrderTime - now).TotalSeconds));
            error = Loc.GetString("centcomm-supply-error-cooldown", ("seconds", secondsCount));
            return false;
        }

        var station = GetEntity(stationNet);
        if (ent.Comp.SelectedStation != station)
        {
            error = Loc.GetString("centcomm-supply-error-invalid-station");
            return false;
        }

        var owningStation = _station.GetOwningStation(ent.Owner);
        if (station == owningStation
            || !TryComp<StationDataComponent>(station, out var stationData)
            || !HasBuyPallets(stationData)
            || !TryComp<StationCargoOrderDatabaseComponent>(station, out _))
        {
            error = Loc.GetString("centcomm-supply-error-invalid-station");
            return false;
        }

        if (entries == null || entries.Count == 0)
        {
            error = Loc.GetString("centcomm-supply-error-empty");
            return false;
        }

        if (entries.Count > ent.Comp.MaxProductsPerOrder)
        {
            error = Loc.GetString("centcomm-supply-error-too-many-products",
                ("max", ent.Comp.MaxProductsPerOrder));
            return false;
        }

        var seen = new HashSet<string>();
        var orderPlan = new OrderPlan
        {
            Station = station,
        };

        var totalItems = 0L;
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.ProductId))
            {
                error = Loc.GetString("centcomm-supply-error-unknown-product");
                return false;
            }

            if (!seen.Add(entry.ProductId))
            {
                error = Loc.GetString("centcomm-supply-error-duplicate");
                return false;
            }

            if (!_prototypes.TryIndex(entry.ProductId, out CentCommSupplyProductPrototype? product)
                || !product.Enabled)
            {
                error = Loc.GetString("centcomm-supply-error-unknown-product");
                return false;
            }

            if (entry.Quantity < 1 || entry.Quantity > product.MaxQuantity)
            {
                error = Loc.GetString("centcomm-supply-error-invalid-quantity",
                    ("product", Loc.GetString(product.Name)));
                return false;
            }

            if (!IsProductValid(product))
            {
                error = Loc.GetString("centcomm-supply-error-invalid-product");
                return false;
            }

            orderPlan.ProductSummary.Add(Loc.GetString("centcomm-supply-paper-product",
                ("product", Loc.GetString(product.Name))));
            orderPlan.ProductSummary.Add(Loc.GetString("centcomm-supply-paper-quantity",
                ("quantity", entry.Quantity)));

            foreach (var content in product.Contents)
            {
                var amount = (long) content.Amount * entry.Quantity;
                if (amount <= 0 || amount > int.MaxValue)
                {
                    error = Loc.GetString("centcomm-supply-error-invalid-product");
                    return false;
                }

                totalItems += amount;
                orderPlan.Items.Add((content.Id, (int) amount));
            }
        }

        if (totalItems <= 0)
        {
            error = Loc.GetString("centcomm-supply-error-empty");
            return false;
        }

        if (!_prototypes.TryIndex(ent.Comp.CratePrototype, out EntityPrototype? crateProto)
            || !crateProto.TryComp<EntityStorageComponent>(out var storageProto, EntityManager.ComponentFactory)
            || storageProto.Capacity <= 0)
        {
            error = Loc.GetString("centcomm-supply-error-bad-config");
            return false;
        }

        var requiredCratesLong = (totalItems + storageProto.Capacity - 1) / storageProto.Capacity;
        if (requiredCratesLong > ent.Comp.MaxCratesPerOrder)
        {
            error = Loc.GetString("centcomm-supply-error-too-many-crates",
                ("max", ent.Comp.MaxCratesPerOrder));
            return false;
        }

        orderPlan.RequiredCrates = (int) requiredCratesLong;
        var freePallets = GetFreeBuyPallets(station);
        if (freePallets.Count < orderPlan.RequiredCrates)
        {
            error = Loc.GetString("centcomm-supply-error-no-pallets",
                ("required", orderPlan.RequiredCrates),
                ("available", freePallets.Count));
            return false;
        }

        plan = orderPlan;
        return true;
    }

    /// <summary>
    /// Packs a validated order into crates and queues it for delivery.
    /// </summary>
    private bool TryDoPlaceOrder(
        Entity<CentCommSupplyConsoleComponent> ent,
        EntityUid actor,
        Guid orderId,
        OrderPlan plan,
        out string? error)
    {
        if (!TryComp<StationCargoOrderDatabaseComponent>(plan.Station, out var orderDatabase))
        {
            error = Loc.GetString("centcomm-supply-error-invalid-station");
            return false;
        }

        var crates = new List<EntityUid>(plan.RequiredCrates);
        var consoleCoords = MapCoordinates.Nullspace;
        EntityUid? currentCrate = null;
        EntityStorageComponent? currentStorage = null;

        foreach (var (id, amount) in plan.Items)
        {
            for (var i = 0; i < amount; i++)
            {
                if (currentCrate == null
                    || currentStorage == null
                    || currentStorage.Contents.ContainedEntities.Count >= currentStorage.Capacity)
                {
                    currentCrate = Spawn(ent.Comp.CratePrototype, consoleCoords);
                    crates.Add(currentCrate.Value);
                    if (!TryComp<EntityStorageComponent>(currentCrate.Value, out currentStorage))
                    {
                        RollbackCrates(crates);
                        error = Loc.GetString("centcomm-supply-error-bad-config");
                        return false;
                    }
                }

                var item = Spawn(id, consoleCoords);
                if (!_storage.CanInsert(item, currentCrate.Value, currentStorage)
                    || !_storage.Insert(item, currentCrate.Value, currentStorage))
                {
                    QueueDel(item);
                    RollbackCrates(crates);
                    error = Loc.GetString("centcomm-supply-error-pack-failed");
                    return false;
                }
            }
        }

        if (crates.Count != plan.RequiredCrates)
        {
            RollbackCrates(crates);
            error = Loc.GetString("centcomm-supply-error-pack-failed");
            return false;
        }

        var stationName = MetaData(plan.Station).EntityName;
        if (string.IsNullOrWhiteSpace(stationName))
            stationName = Loc.GetString("centcomm-supply-unknown-station");

        var orderNumber = ++orderDatabase.NumOrdersCreated;
        var paperPrototype = orderDatabase.PrinterOutput;
        if (!_prototypes.HasIndex(paperPrototype))
            paperPrototype = "PaperCargoInvoice";

        var deliveryDelay = ent.Comp.DeliveryDelay > TimeSpan.Zero
            ? ent.Comp.DeliveryDelay
            : TimeSpan.Zero;
        var deliveryTimeout = ent.Comp.DeliveryTimeout > TimeSpan.Zero
            ? ent.Comp.DeliveryTimeout
            : TimeSpan.Zero;
        var now = _timing.CurTime;

        _acceptedOrders.Add(orderId);
        _recentOrders[orderId] = now;
        PruneRecentOrders();
        _pendingOrders.Add(new PendingOrder
        {
            Console = ent.Owner,
            Actor = actor,
            Station = plan.Station,
            DeliverAt = now + deliveryDelay,
            ExpireAt = now + deliveryDelay + deliveryTimeout,
            OrderNumber = orderNumber,
            PaperPrototype = paperPrototype,
            StationName = stationName,
            Contents = string.Join("\n", plan.ProductSummary),
            DeliverySound = ent.Comp.DeliverySound,
            Crates = crates,
        });

        ent.Comp.SelectedStation = plan.Station;
        ent.Comp.NextOrderTime = now + ent.Comp.OrderCooldown;
        _audio.PlayPvs(ent.Comp.ApproveSound, ent.Owner);

        var announcement = Loc.GetString(ent.Comp.OrderAnnouncement,
            ("station", stationName));
        _chat.DispatchGlobalAnnouncement(
            announcement,
            sender: Loc.GetString(ent.Comp.AnnouncementSender),
            playDefault: false);

        var summary = string.Join(", ", plan.Items.Select(entry => $"{entry.Id} x{entry.Amount}"));
        _adminLog.Add(
            LogType.Action,
            LogImpact.High,
            $"{ToPrettyString(actor)} ordered CentComm supplies #{orderNumber} via {ToPrettyString(ent)} for {ToPrettyString(plan.Station)}: {summary}");

        var accepted = Loc.GetString("centcomm-supply-order-accepted");
        _popup.PopupEntity(accepted, ent.Owner, actor, PopupType.Medium);
        UpdateUi(ent, accepted, orderPlaced: true);

        error = null;
        return true;
    }

    /// <summary>
    /// Sends the current console state to the client.
    /// </summary>
    private void UpdateUi(Entity<CentCommSupplyConsoleComponent> ent, string? status = null, bool orderPlaced = false)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return;

        var stations = GetOrderableStations(ent);
        var available = new Dictionary<NetEntity, string>();
        foreach (var station in stations)
        {
            var name = MetaData(station).EntityName;
            if (string.IsNullOrWhiteSpace(name))
                name = Loc.GetString("centcomm-supply-unknown-station");
            available[GetNetEntity(station)] = name;
        }

        var selected = ResolveSelectedStation(ent, stations, available);
        var products = new List<CentCommSupplyProductEntry>();

        foreach (var product in _prototypes.EnumeratePrototypes<CentCommSupplyProductPrototype>()
                     .OrderBy(product => product.SortOrder)
                     .ThenBy(product => product.ID))
        {
            if (!product.Enabled || !IsProductValid(product))
                continue;

            products.Add(new CentCommSupplyProductEntry(
                product.ID,
                Loc.GetString(product.Name),
                product.Description is { } description ? Loc.GetString(description) : null,
                Loc.GetString(product.Category),
                Math.Max(1, product.MaxQuantity)));
        }

        var state = new CentCommSupplyBoundUserInterfaceState(
            products,
            available,
            selected is { } selectedStation ? GetNetEntity(selectedStation) : null,
            ent.Comp.MaxCratesPerOrder,
            ent.Comp.MaxProductsPerOrder,
            ent.Comp.LastStationSelectionId,
            status,
            orderPlaced);

        _ui.SetUiState(ent.Owner, CentCommSupplyUiKey.Key, state);
    }

    /// <summary>
    /// Places queued crates on the provided free pallets and attaches invoices.
    /// </summary>
    private bool TryDeliver(
        PendingOrder order,
        List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent Transform)> freePallets)
    {
        _random.Shuffle(freePallets);

        for (var i = 0; i < order.Crates.Count; i++)
        {
            var crate = order.Crates[i];
            if (TerminatingOrDeleted(crate))
                return false;

            var pallet = freePallets[i];
            _transform.SetCoordinates(crate,
                new EntityCoordinates(pallet.Transform.ParentUid, pallet.Transform.LocalPosition));
            _transform.SetLocalRotation(crate, pallet.Transform.LocalRotation);
        }

        foreach (var crate in order.Crates)
            AttachInvoice(crate, order);

        _audio.PlayGlobal(order.DeliverySound,
            Filter.Empty().AddWhere(_gameTicker.UserHasJoinedGame),
            true);

        _adminLog.Add(
            LogType.Action,
            LogImpact.Low,
            $"Delivered CentComm supply order #{order.OrderNumber} to {ToPrettyString(order.Station)} ({order.Crates.Count} crates)");

        return true;
    }

    /// <summary>
    /// Spawns and inserts a paper invoice into the crate's label slot.
    /// </summary>
    private void AttachInvoice(EntityUid crate, PendingOrder order)
    {
        var paper = Spawn(order.PaperPrototype, Transform(crate).Coordinates);
        if (TryComp<PaperComponent>(paper, out var paperComponent))
        {
            _meta.SetEntityName(paper,
                Loc.GetString("centcomm-supply-paper-name", ("orderNumber", order.OrderNumber)));
            _paper.SetContent((paper, paperComponent), Loc.GetString("centcomm-supply-paper-text",
                ("orderNumber", order.OrderNumber),
                ("contents", order.Contents)));
        }

        if (TryComp<PaperLabelComponent>(crate, out var label)
            && !_slots.TryInsert(crate, label.LabelSlot, paper, null))
        {
            Log.Warning($"Failed to attach CentComm supply invoice to {ToPrettyString(crate)}.");
        }
    }

    /// <summary>
    /// Resolves the station that should be selected in the console UI.
    /// </summary>
    private EntityUid? ResolveSelectedStation(
        Entity<CentCommSupplyConsoleComponent> ent,
        List<EntityUid> stations,
        Dictionary<NetEntity, string> available)
    {
        if (ent.Comp.SelectedStation is { } selected
            && available.ContainsKey(GetNetEntity(selected)))
        {
            return selected;
        }

        foreach (var station in stations)
        {
            if (!available.ContainsKey(GetNetEntity(station)))
                continue;

            ent.Comp.SelectedStation = station;
            return station;
        }

        ent.Comp.SelectedStation = null;
        return null;
    }

    /// <summary>
    /// Returns all stations that can receive CentComm supply orders.
    /// </summary>
    private List<EntityUid> GetOrderableStations(Entity<CentCommSupplyConsoleComponent> ent)
    {
        var stations = new List<EntityUid>();
        var owningStation = _station.GetOwningStation(ent.Owner);

        foreach (var station in _station.GetStations())
        {
            if (station == owningStation)
                continue;

            if (!TryComp<StationDataComponent>(station, out var data))
                continue;

            if (!TryComp<StationCargoOrderDatabaseComponent>(station, out _))
                continue;

            if (HasBuyPallets(data))
                stations.Add(station);
        }

        return stations;
    }

    /// <summary>
    /// Checks whether a station has at least one buying cargo pallet.
    /// </summary>
    private bool HasBuyPallets(StationDataComponent data)
    {
        foreach (var grid in data.Grids)
        {
            if (GetCargoPallets(grid, BuySellType.Buy).Count > 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns all free buying pallets belonging to a station.
    /// </summary>
    private List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent Transform)> GetFreeBuyPallets(
        EntityUid station)
    {
        var result = new List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent Transform)>();

        if (!TryComp<StationDataComponent>(station, out var data))
            return result;

        foreach (var grid in data.Grids)
        {
            var pallets = GetCargoPallets(grid, BuySellType.Buy);
            if (pallets.Count == 0)
                continue;

            result.AddRange(GetFreeCargoPallets(grid, pallets));
        }

        return result;
    }

    /// <summary>
    /// Collects anchored cargo pallets of the requested type on a grid.
    /// </summary>
    private List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent Transform)> GetCargoPallets(
        EntityUid gridUid,
        BuySellType requestType)
    {
        _pads.Clear();

        var query = AllEntityQuery<CargoPalletComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var component, out var xform))
        {
            if (xform.ParentUid != gridUid || !xform.Anchored)
                continue;

            if ((requestType & component.PalletType) == 0)
                continue;

            _pads.Add((uid, component, xform));
        }

        return _pads;
    }

    /// <summary>
    /// Filters out pallets occupied by dynamic entities or static obstacles that would block a crate.
    /// </summary>
    private List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent Transform)> GetFreeCargoPallets(
        EntityUid gridUid,
        List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent Transform)> pallets)
    {
        var result = new List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent Transform)>();

        foreach (var pallet in pallets)
        {
            var aabb = _lookup.GetAABBNoContainer(
                pallet.Entity,
                pallet.Transform.LocalPosition,
                pallet.Transform.LocalRotation);

            if (_lookup.AnyLocalEntitiesIntersecting(
                    gridUid,
                    aabb,
                    LookupFlags.Dynamic,
                    pallet.Entity))
            {
                continue;
            }

            _staticIntersecting.Clear();
            _lookup.GetLocalEntitiesIntersecting(
                gridUid,
                aabb,
                _staticIntersecting,
                LookupFlags.Static);

            var blocked = false;
            foreach (var uid in _staticIntersecting)
            {
                if (uid == pallet.Entity || !HasCrateBlockingFixture(uid))
                    continue;

                blocked = true;
                break;
            }

            if (blocked)
                continue;

            result.Add(pallet);
        }

        return result;
    }

    /// <summary>
    /// Checks whether a static entity has a hard fixture that can physically block a crate.
    /// </summary>
    private bool HasCrateBlockingFixture(EntityUid uid)
    {
        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return false;

        var crateMask = (int) CollisionGroup.CrateMask;
        var crateLayer = (int) CollisionGroup.MachineLayer;

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (fixture.Hard &&
                (fixture.CollisionLayer & crateMask) != 0 &&
                (fixture.CollisionMask & crateLayer) != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks that a product has a positive quantity and valid contents.
    /// </summary>
    private bool IsProductValid(CentCommSupplyProductPrototype product)
    {
        if (product.MaxQuantity <= 0 || product.Contents == null || product.Contents.Count == 0)
            return false;

        foreach (var content in product.Contents)
        {
            if (content.Amount <= 0 || !_prototypes.HasIndex(content.Id))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Limits the recent-order cache while preserving accepted order identifiers.
    /// </summary>
    private void PruneRecentOrders()
    {
        if (_recentOrders.Count <= 512)
            return;

        foreach (var (orderId, _) in _recentOrders.OrderBy(pair => pair.Value).ToList())
        {
            if (_recentOrders.Count <= 512)
                break;

            if (_acceptedOrders.Contains(orderId))
                continue;

            _recentOrders.Remove(orderId);
        }
    }

    /// <summary>
    /// Notifies the ordering actor that delivery timed out.
    /// </summary>
    private void NotifyDeliveryTimeout(PendingOrder order)
    {
        Log.Warning($"CentComm supply order #{order.OrderNumber} timed out waiting for free cargo pallets.");

        if (TerminatingOrDeleted(order.Console)
            || !TryComp<CentCommSupplyConsoleComponent>(order.Console, out var component))
        {
            return;
        }

        var message = Loc.GetString("centcomm-supply-error-delivery-timeout");
        if (!TerminatingOrDeleted(order.Actor))
            _popup.PopupEntity(message, order.Console, order.Actor, PopupType.MediumCaution);

        UpdateUi((order.Console, component), message);
    }

    /// <summary>
    /// Deletes all crates and their contents after a failed order.
    /// </summary>
    private void RollbackCrates(List<EntityUid> crates)
    {
        foreach (var crate in crates)
        {
            if (TerminatingOrDeleted(crate))
                continue;

            if (TryComp<EntityStorageComponent>(crate, out var storage))
                storage.DeleteContentsOnDestruction = true;

            QueueDel(crate);
        }
    }

    /// <summary>
    /// Shows an error to the actor and sends it to the console UI.
    /// </summary>
    private void Fail(Entity<CentCommSupplyConsoleComponent> ent, EntityUid actor, string message)
    {
        _popup.PopupEntity(message, ent.Owner, actor, PopupType.MediumCaution);
        UpdateUi(ent, message);
    }

    private sealed class PendingOrder
    {
        public EntityUid Console;
        public EntityUid Actor;
        public EntityUid Station;
        public TimeSpan DeliverAt;
        public TimeSpan ExpireAt;
        public int OrderNumber;
        public EntProtoId PaperPrototype;
        public string StationName = string.Empty;
        public string Contents = string.Empty;
        public SoundSpecifier? DeliverySound;
        public List<EntityUid> Crates = new();
    }

    private sealed class OrderPlan
    {
        public EntityUid Station;
        public List<(EntProtoId Id, int Amount)> Items = new();
        public List<string> ProductSummary = new();
        public int RequiredCrates;
    }
}
