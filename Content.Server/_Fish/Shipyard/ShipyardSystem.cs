using System.Linq;
using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Cargo.Systems;
using Content.Server.Popups;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Fish.Shipyard;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Database;
using Content.Shared.Station.Components;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Fish.Shipyard;

/// <summary>
/// Серверная логика консоли верфи: каталог, оплата со счёта станции, спавн и стыковка.
/// </summary>
public sealed partial class ShipyardSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private CargoSystem _cargo = default!;
    [Dependency] private DockingSystem _docking = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private ShuttleSystem _shuttles = default!;
    [Dependency] private StationSystem _station = default!;

    private ISawmill _sawmill = default!;
    private float _spawnIndex;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("fish-shipyard");

        Subs.BuiEvents<ShipyardConsoleComponent>(ShipyardConsoleUiKey.Shipyard, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<ShipyardConsolePurchaseMessage>(OnPurchaseMessage);
        });
    }

    private void OnUiOpened(Entity<ShipyardConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnPurchaseMessage(Entity<ShipyardConsoleComponent> ent, ref ShipyardConsolePurchaseMessage args)
    {
        TryPurchase(ent, args.Actor, args.VesselId);
    }

    /// <summary>
    /// Публичный API покупки: проверки + выполнение.
    /// </summary>
    public bool TryPurchase(Entity<ShipyardConsoleComponent> ent, EntityUid actor, string vesselId)
    {
        if (!Exists(actor))
            return false;

        if (!CanPurchase(ent, actor, vesselId, out var vessel, out var stationUid, out var bank, out var targetGrid, out var reason))
        {
            Deny(ent, actor, reason ?? Loc.GetString("shipyard-console-invalid-vessel"));
            return false;
        }

        return DoPurchase(ent, actor, vessel!, stationUid!.Value, bank!, targetGrid!.Value);
    }

    /// <summary>
    /// Чистая проверка возможности покупки. Не меняет состояние сущностей.
    /// </summary>
    public bool CanPurchase(
        Entity<ShipyardConsoleComponent> ent,
        EntityUid actor,
        string vesselId,
        out VesselPrototype? vessel,
        out EntityUid? stationUid,
        out StationBankAccountComponent? bank,
        out EntityUid? targetGrid,
        out string? reason,
        bool quiet = true)
    {
        vessel = null;
        stationUid = null;
        bank = null;
        targetGrid = null;
        reason = null;

        if (ent.Comp.Purchasing)
        {
            reason = Loc.GetString("shipyard-console-busy");
            return false;
        }

        if (TryComp(ent, out Content.Shared.Access.Components.AccessReaderComponent? accessReader) &&
            !_access.IsAllowed(actor, ent, accessReader))
        {
            reason = Loc.GetString("shipyard-console-access-denied");
            return false;
        }

        if (!_prototypes.TryIndex<VesselPrototype>(vesselId, out vessel))
        {
            reason = Loc.GetString("shipyard-console-invalid-vessel");
            return false;
        }

        if (!string.Equals(vessel.Group, ent.Comp.VesselGroup, StringComparison.OrdinalIgnoreCase))
        {
            reason = Loc.GetString("shipyard-console-invalid-vessel");
            if (!quiet)
            {
                _adminLog.Add(LogType.Action, LogImpact.Medium,
                    $"{ToPrettyString(actor)} tried to buy vessel {vessel.ID} not listed on {ToPrettyString(ent)}");
            }
            return false;
        }

        if (vessel.Price <= 0 || vessel.ShuttlePath == ResPath.Empty)
        {
            reason = Loc.GetString("shipyard-console-invalid-vessel");
            return false;
        }

        var station = _station.GetOwningStation(ent);
        if (station is not { } ownedStation ||
            !TryComp<StationDataComponent>(ownedStation, out var stationData) ||
            !TryComp<StationBankAccountComponent>(ownedStation, out bank))
        {
            reason = Loc.GetString("shipyard-console-no-station");
            return false;
        }

        stationUid = ownedStation;
        targetGrid = _station.GetLargestGrid((ownedStation, stationData));
        if (targetGrid == null)
        {
            reason = Loc.GetString("shipyard-console-no-station");
            return false;
        }

        var balance = _cargo.GetBalanceFromAccount((ownedStation, bank), ent.Comp.Account);
        if (balance < vessel.Price)
        {
            reason = Loc.GetString("shipyard-console-insufficient-funds", ("cost", vessel.Price));
            return false;
        }

        return true;
    }

    private bool DoPurchase(
        Entity<ShipyardConsoleComponent> ent,
        EntityUid actor,
        VesselPrototype vessel,
        EntityUid stationUid,
        StationBankAccountComponent bank,
        EntityUid targetGrid)
    {
        ent.Comp.Purchasing = true;
        string? finalStatus = Loc.GetString("shipyard-console-purchasing");
        UpdateUi(ent, finalStatus);

        try
        {
            if (!TrySpawnAndDockShuttle(vessel, targetGrid, ent.Comp.DefaultPriorityDockTag, out var shuttle, out var failReason))
            {
                finalStatus = failReason ?? Loc.GetString("shipyard-console-spawn-failed");
                Deny(ent, actor, finalStatus);
                return false;
            }

            // Списываем только после успешного спавна и стыковки.
            _cargo.UpdateBankAccount((stationUid, bank), -vessel.Price, ent.Comp.Account);

            var vesselName = Loc.GetString(vessel.Name);
            _metaData.SetEntityName(shuttle, vesselName);

            _adminLog.Add(LogType.Action, LogImpact.High,
                $"{ToPrettyString(actor)} purchased vessel {vessel.ID} ({vesselName}) for {vessel.Price} via {ToPrettyString(ent)}; shuttle={ToPrettyString(shuttle)}");

            _audio.PlayPvs(ent.Comp.ApproveSound, ent);
            finalStatus = Loc.GetString("shipyard-console-purchase-success", ("vessel", vesselName));
            _popup.PopupEntity(finalStatus, actor, actor);
            return true;
        }
        finally
        {
            ent.Comp.Purchasing = false;
            // Сохраняем итоговый статус; не затираем success/error повторным UpdateUi(null).
            if (_ui.IsUiOpen(ent.Owner, ShipyardConsoleUiKey.Shipyard))
                UpdateUi(ent, finalStatus);
        }
    }

    /// <summary>
    /// Загружает шаттл на FTL-карту и мгновенно стыкует к станции по свободному доку.
    /// </summary>
    private bool TrySpawnAndDockShuttle(
        VesselPrototype vessel,
        EntityUid targetGrid,
        string? defaultDockTag,
        out EntityUid shuttleUid,
        out string? failReason)
    {
        shuttleUid = EntityUid.Invalid;
        failReason = null;

        var ftlMap = _shuttles.EnsureSunriseFtlMap();
        var xformMap = Transform(ftlMap);

        if (!_loader.TryLoadGrid(xformMap.MapID, vessel.ShuttlePath, out var loaded) || loaded == null)
        {
            _sawmill.Error($"Failed to load shipyard shuttle {vessel.ShuttlePath}");
            failReason = Loc.GetString("shipyard-console-spawn-failed");
            return false;
        }

        var shuttle = loaded.Value.Owner;
        if (!HasComp<ShuttleComponent>(shuttle) ||
            !TryComp<MapGridComponent>(shuttle, out var grid))
        {
            QueueDel(shuttle);
            failReason = Loc.GetString("shipyard-console-spawn-failed");
            return false;
        }

        var width = grid.LocalAABB.Width;
        var shuttleCenter = grid.LocalAABB.Center;
        var coordinates = new EntityCoordinates(ftlMap, new Vector2(500f + _spawnIndex + width / 2f, 1f) - shuttleCenter);
        _transform.SetCoordinates(shuttle, coordinates);
        _spawnIndex += width + 5f;

        var priorityTag = vessel.PriorityDockTag ?? defaultDockTag;
        var config = !string.IsNullOrEmpty(priorityTag)
            ? _docking.GetDockingConfig(shuttle, targetGrid, priorityTag)
            : null;

        // Если приоритетный док занят — любой свободный стыковочный порт станции.
        config ??= _docking.GetDockingConfig(shuttle, targetGrid);

        if (config == null)
        {
            QueueDel(shuttle);
            failReason = Loc.GetString("shipyard-console-no-dock");
            return false;
        }

        _shuttles.FTLDock((shuttle, Transform(shuttle)), config);

        shuttleUid = shuttle;
        return true;
    }

    private void Deny(Entity<ShipyardConsoleComponent> ent, EntityUid actor, string message)
    {
        _audio.PlayPvs(ent.Comp.DenySound, ent);
        _popup.PopupEntity(message, actor, actor);
        UpdateUi(ent, message);
    }

    private void UpdateUi(Entity<ShipyardConsoleComponent> ent, string? status = null)
    {
        var balance = 0;
        var station = _station.GetOwningStation(ent);
        if (station is { } stationUid && TryComp<StationBankAccountComponent>(stationUid, out var bank))
            balance = _cargo.GetBalanceFromAccount((stationUid, bank), ent.Comp.Account);

        var vessels = new List<ShipyardVesselEntry>();
        foreach (var vessel in _prototypes.EnumeratePrototypes<VesselPrototype>()
                     .Where(v => string.Equals(v.Group, ent.Comp.VesselGroup, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(v => Loc.GetString(v.Name), StringComparer.CurrentCultureIgnoreCase))
        {
            var name = Loc.GetString(vessel.Name);
            var description = string.IsNullOrWhiteSpace(vessel.Description)
                ? string.Empty
                : Loc.GetString(vessel.Description);

            string? unavailable = null;
            var canPurchase = !ent.Comp.Purchasing;
            if (vessel.Price > balance)
            {
                canPurchase = false;
                unavailable = Loc.GetString("shipyard-console-insufficient-funds", ("cost", vessel.Price));
            }

            vessels.Add(new ShipyardVesselEntry(
                vessel.ID,
                name,
                description,
                vessel.Price,
                canPurchase,
                unavailable));
        }

        _ui.SetUiState(ent.Owner, ShipyardConsoleUiKey.Shipyard,
            new ShipyardConsoleBoundUserInterfaceState(balance, status, ent.Comp.Purchasing, vessels));
    }
}
