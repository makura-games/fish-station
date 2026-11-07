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
public sealed partial class CentCommSupplySystem : EntitySystem
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
    private readonly List<PendingOrder> _pendingOrders = new();
    private readonly List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent Transform)> _pads = new();

    private sealed class PendingOrder
    {
        public EntityUid Station;
        public TimeSpan DeliverAt;
        public int OrderNumber;
        public EntProtoId PaperPrototype;
        public string StationName = string.Empty;
        public string Contents = string.Empty;
        public SoundSpecifier? DeliverySound;
        public List<EntityUid> Crates = new();
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<CentCommSupplyConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CentCommSupplyConsoleComponent, CentCommSupplySelectStationMessage>(OnSelectStation);
        SubscribeLocalEvent<CentCommSupplyConsoleComponent, CentCommSupplyOrderMessage>(OnOrder);
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

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        foreach (var order in _pendingOrders)
            RollbackCrates(order.Crates);

        _pendingOrders.Clear();
        _recentOrders.Clear();
    }

    private void OnUiOpened(Entity<CentCommSupplyConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnSelectStation(Entity<CentCommSupplyConsoleComponent> ent, ref CentCommSupplySelectStationMessage args)
    {
        if (!_access.IsAllowed(args.Actor, ent.Owner))
        {
            Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-access"));
            return;
        }

        var station = GetEntity(args.Station);
        var owningStation = _station.GetOwningStation(ent.Owner);
        if (station == owningStation
            || !TryComp<StationDataComponent>(station, out var data)
            || !HasBuyPallets(data))
        {
            Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-invalid-station"));
            return;
        }

        ent.Comp.SelectedStation = station;
        UpdateUi(ent);
    }

    private void OnOrder(Entity<CentCommSupplyConsoleComponent> ent, ref CentCommSupplyOrderMessage args)
    {
        if (!_access.IsAllowed(args.Actor, ent.Owner))
        {
            Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-access"));
            return;
        }

        if (args.OrderId == Guid.Empty || _recentOrders.ContainsKey(args.OrderId))
            return;

        _recentOrders[args.OrderId] = _timing.CurTime;
        if (_recentOrders.Count > 512)
            _recentOrders.Clear();

        var now = _timing.CurTime;
        if (now < ent.Comp.NextOrderTime)
        {
            var secondsCount = Math.Max(1, (int) Math.Ceiling((ent.Comp.NextOrderTime - now).TotalSeconds));
            Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-cooldown", ("seconds", secondsCount)));
            return;
        }

        var station = GetEntity(args.Station);
        var owningStation = _station.GetOwningStation(ent.Owner);
        if (station == owningStation
            || !TryComp<StationDataComponent>(station, out var stationData)
            || !HasBuyPallets(stationData)
            || !TryComp<StationCargoOrderDatabaseComponent>(station, out var orderDatabase))
        {
            Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-invalid-station"));
            return;
        }

        if (args.Entries == null || args.Entries.Count == 0)
        {
            Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-empty"));
            return;
        }

        if (args.Entries.Count > ent.Comp.MaxProductsPerOrder)
        {
            Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-too-many-products",
                ("max", ent.Comp.MaxProductsPerOrder)));
            return;
        }

        var seen = new HashSet<string>();
        var items = new List<(EntProtoId Id, int Amount)>();
        var productSummary = new List<string>();
        var totalItems = 0L;

        foreach (var entry in args.Entries)
        {
            if (string.IsNullOrEmpty(entry.ProductId))
            {
                Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-unknown-product"));
                return;
            }

            if (!seen.Add(entry.ProductId))
            {
                Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-duplicate"));
                return;
            }

            if (!_prototypes.TryIndex(entry.ProductId, out CentCommSupplyProductPrototype? product)
                || !product.Enabled)
            {
                Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-unknown-product"));
                return;
            }

            if (entry.Quantity < 1 || entry.Quantity > product.MaxQuantity)
            {
                Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-invalid-quantity",
                    ("product", Loc.GetString(product.Name))));
                return;
            }

            if (!IsProductValid(product))
            {
                Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-invalid-product"));
                return;
            }

            productSummary.Add(Loc.GetString("centcomm-supply-paper-product",
                ("product", Loc.GetString(product.Name))));
            productSummary.Add(Loc.GetString("centcomm-supply-paper-quantity",
                ("quantity", entry.Quantity)));

            foreach (var content in product.Contents)
            {
                var amount = (long) content.Amount * entry.Quantity;
                if (amount <= 0 || amount > int.MaxValue)
                {
                    Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-invalid-product"));
                    return;
                }

                totalItems += amount;
                items.Add((content.Id, (int) amount));
            }
        }

        if (totalItems <= 0)
        {
            Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-empty"));
            return;
        }

        if (!_prototypes.TryIndex(ent.Comp.CratePrototype, out EntityPrototype? crateProto)
            || !crateProto.TryComp<EntityStorageComponent>(out var storageProto, EntityManager.ComponentFactory)
            || storageProto.Capacity <= 0)
        {
            Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-bad-config"));
            return;
        }

        var requiredCratesLong = (totalItems + storageProto.Capacity - 1) / storageProto.Capacity;
        if (requiredCratesLong > ent.Comp.MaxCratesPerOrder)
        {
            Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-too-many-crates",
                ("max", ent.Comp.MaxCratesPerOrder)));
            return;
        }

        var requiredCrates = (int) requiredCratesLong;
        var freePallets = GetFreeBuyPallets(station);
        if (freePallets.Count < requiredCrates)
        {
            Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-no-pallets",
                ("required", requiredCrates),
                ("available", freePallets.Count)));
            return;
        }

        var crates = new List<EntityUid>(requiredCrates);
        var consoleCoords = MapCoordinates.Nullspace;
        EntityUid? currentCrate = null;
        EntityStorageComponent? currentStorage = null;

        foreach (var (id, amount) in items)
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
                        Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-bad-config"));
                        return;
                    }
                }

                var item = Spawn(id, consoleCoords);
                if (!_storage.CanInsert(item, currentCrate.Value, currentStorage)
                    || !_storage.Insert(item, currentCrate.Value, currentStorage))
                {
                    QueueDel(item);
                    RollbackCrates(crates);
                    Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-pack-failed"));
                    return;
                }
            }
        }

        if (crates.Count != requiredCrates)
        {
            RollbackCrates(crates);
            Fail(ent, args.Actor, Loc.GetString("centcomm-supply-error-pack-failed"));
            return;
        }

        var stationName = MetaData(station).EntityName;
        if (string.IsNullOrWhiteSpace(stationName))
            stationName = Loc.GetString("centcomm-supply-unknown-station");

        var orderNumber = ++orderDatabase.NumOrdersCreated;
        var paperPrototype = orderDatabase.PrinterOutput;
        if (!_prototypes.HasIndex(paperPrototype))
            paperPrototype = "PaperCargoInvoice";

        var deliveryDelay = ent.Comp.DeliveryDelay > TimeSpan.Zero
            ? ent.Comp.DeliveryDelay
            : TimeSpan.Zero;

        _pendingOrders.Add(new PendingOrder
        {
            Station = station,
            DeliverAt = now + deliveryDelay,
            OrderNumber = orderNumber,
            PaperPrototype = paperPrototype,
            StationName = stationName,
            Contents = string.Join("\n", productSummary),
            DeliverySound = ent.Comp.DeliverySound,
            Crates = crates,
        });

        ent.Comp.SelectedStation = station;
        ent.Comp.NextOrderTime = now + ent.Comp.OrderCooldown;
        _audio.PlayPvs(ent.Comp.ApproveSound, ent.Owner);

        var announcement = Loc.GetString(ent.Comp.OrderAnnouncement,
            ("station", stationName));
        _chat.DispatchGlobalAnnouncement(
            announcement,
            sender: Loc.GetString(ent.Comp.AnnouncementSender),
            playDefault: false);

        var summary = string.Join(", ", args.Entries.Select(entry => $"{entry.ProductId} x{entry.Quantity}"));
        _adminLog.Add(
            LogType.Action,
            LogImpact.High,
            $"{ToPrettyString(args.Actor)} ordered CentComm supplies #{orderNumber} via {ToPrettyString(ent)} for {ToPrettyString(station)}: {summary}");

        var accepted = Loc.GetString("centcomm-supply-order-accepted");
        _popup.PopupEntity(accepted, ent.Owner, args.Actor, PopupType.Medium);
        UpdateUi(ent, accepted, orderPlaced: true);
    }

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
            status,
            orderPlaced);

        _ui.SetUiState(ent.Owner, CentCommSupplyUiKey.Key, state);
    }

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

            if (HasBuyPallets(data))
                stations.Add(station);
        }

        return stations;
    }

    private bool HasBuyPallets(StationDataComponent data)
    {
        foreach (var grid in data.Grids)
        {
            if (GetCargoPallets(grid, BuySellType.Buy).Count > 0)
                return true;
        }

        return false;
    }

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

            if (_lookup.AnyLocalEntitiesIntersecting(gridUid, aabb, LookupFlags.Dynamic))
                continue;

            result.Add(pallet);
        }

        return result;
    }

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

    private void Fail(Entity<CentCommSupplyConsoleComponent> ent, EntityUid actor, string message)
    {
        _popup.PopupEntity(message, ent.Owner, actor, PopupType.MediumCaution);
        UpdateUi(ent, message);
    }
}
