using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Spider;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;

namespace Content.Shared._Fish.Spider;

/// <summary>
/// Обрабатывает откладывание паучьих яиц на паутине за счёт голода.
/// </summary>
public sealed partial class FishSpiderEggLayerSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SatiationSystem _satiation = default!;

    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private EntityQuery<SatiationComponent> _satiationQuery = default!;
    [Dependency] private EntityQuery<SpiderWebObjectComponent> _webQuery = default!;
    [Dependency] private EntityQuery<FishSpiderEggComponent> _eggQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishSpiderEggLayerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FishSpiderEggLayerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FishSpiderEggLayerComponent, FishSpiderLayEggActionEvent>(OnLayEggAction);
        SubscribeLocalEvent<FishSpiderEggLayerComponent, FishSpiderLayEggDoAfterEvent>(OnLayEggDoAfter);
    }

    private void OnMapInit(Entity<FishSpiderEggLayerComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
        Dirty(ent);
    }

    private void OnShutdown(Entity<FishSpiderEggLayerComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnLayEggAction(Entity<FishSpiderEggLayerComponent> ent, ref FishSpiderLayEggActionEvent args)
    {
        if (args.Handled)
            return;

        if (TryStartLayEgg(ent))
            args.Handled = true;
    }

    private void OnLayEggDoAfter(Entity<FishSpiderEggLayerComponent> ent, ref FishSpiderLayEggDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Deleted)
            return;

        if (TryLayEgg(ent))
            args.Handled = true;
    }

    /// <summary>
    /// Запускает процесс откладывания яйца, если все условия выполнены.
    /// </summary>
    public bool TryStartLayEgg(Entity<FishSpiderEggLayerComponent> ent)
    {
        if (!CanLayEgg(ent))
            return false;

        var doAfterArgs = new DoAfterArgs(EntityManager, ent, ent.Comp.LayDelay, new FishSpiderLayEggDoAfterEvent(), ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
        };

        return _doAfter.TryStartDoAfter(doAfterArgs);
    }

    /// <summary>
    /// Завершает откладывание яйца: списывает голод и создаёт прикреплённое яйцо на тайле с паутиной.
    /// </summary>
    public bool TryLayEgg(Entity<FishSpiderEggLayerComponent> ent)
    {
        if (!CanLayEgg(ent))
            return false;

        if (!_satiationQuery.TryComp(ent, out var satiation))
            return false;

        _satiation.ModifyValue((ent, satiation), SatiationSystem.Hunger, -ent.Comp.HungerCost);

        if (_net.IsServer)
        {
            var xform = Transform(ent);
            var spawnCoords = xform.Coordinates;
            if (xform.GridUid is { } gridUid && _gridQuery.TryComp(gridUid, out var grid))
            {
                var tileIndices = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
                spawnCoords = _map.GridTileToLocal(gridUid, grid, tileIndices);
            }

            Spawn(ent.Comp.EggPrototype, spawnCoords);
        }

        _popup.PopupEntity(Loc.GetString("fish-spider-egg-laid"), ent, ent);
        return true;
    }

    /// <summary>
    /// Проверяет, может ли паук отложить яйцо в текущей позиции.
    /// </summary>
    public bool CanLayEgg(Entity<FishSpiderEggLayerComponent> ent, bool quiet = false)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("fish-spider-egg-no-grid"), ent, ent);
            return false;
        }

        if (!HasWebOnTile(gridUid, grid, xform.Coordinates))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("fish-spider-egg-need-web"), ent, ent);
            return false;
        }

        if (HasEggOnTile(gridUid, grid, xform.Coordinates))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("fish-spider-egg-already-exists"), ent, ent);
            return false;
        }

        if (!_satiationQuery.TryComp(ent, out var satiation) ||
            _satiation.GetValueOrNull((ent, satiation), SatiationSystem.Hunger) is not { } currentHunger ||
            _satiation.GetMaximumValue((ent, satiation), SatiationSystem.Hunger) is not { } maxHunger ||
            maxHunger <= 0 ||
            currentHunger / maxHunger < ent.Comp.MinHungerRatio)
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("fish-spider-egg-hungry"), ent, ent);
            return false;
        }

        return true;
    }

    private bool HasWebOnTile(EntityUid gridUid, MapGridComponent grid, EntityCoordinates coords)
    {
        var enumerator = _map.GetAnchoredEntities(gridUid, grid, coords);
        while (enumerator.MoveNext(out var entity))
        {
            if (_webQuery.HasComp(entity.Value))
                return true;
        }

        return false;
    }

    private bool HasEggOnTile(EntityUid gridUid, MapGridComponent grid, EntityCoordinates coords)
    {
        var enumerator = _map.GetAnchoredEntities(gridUid, grid, coords);
        while (enumerator.MoveNext(out var entity))
        {
            if (_eggQuery.HasComp(entity.Value))
                return true;
        }

        return false;
    }
}
