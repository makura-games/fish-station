using Content.Shared._Fish.ClockworkCult;
using Content.Shared._Fish.ClockworkCult.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.ClockworkCult.Systems;

/// <summary>
/// Replica Fabricator: листы → power, производство латуни, конвертация полов/стен.
/// </summary>
public sealed partial class FishReplicaFabricatorSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private FishClockworkPowerSystem _power = default!;
    [Dependency] private FishClockworkServantSystem _servant = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private TurfSystem _turf = default!;

    private static readonly EntProtoId SheetBrass = "SheetBrass1";

    // Fish-start: whitelist обычных металлических стен (не reinforced/rock/wood).
    private static readonly HashSet<string> ConvertibleWalls = new(StringComparer.Ordinal)
    {
        "WallSolid",
        "WallSolidDiagonal",
        "WallSolidRust",
    };
    // Fish-end

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishReplicaFabricatorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<FishReplicaFabricatorComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<FishReplicaFabricatorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !_servant.IsServant(args.User))
            return;

        var cost = FishClockworkCultConstants.BrassSheetCreateCost;
        if (!_power.TrySpendPower(cost))
        {
            _popup.PopupEntity(Loc.GetString("fish-clockwork-not-enough-power"), args.User, args.User);
            return;
        }

        // 5 листов латуни (tg 1.25 kW).
        for (var i = 0; i < FishClockworkCultConstants.BrassSheetCreateCount; i++)
            Spawn(SheetBrass, Transform(args.User).Coordinates);

        _popup.PopupEntity(Loc.GetString("fish-clockwork-fabricator-brass"), args.User, args.User);
        args.Handled = true;
    }

    private void OnAfterInteract(Entity<FishReplicaFabricatorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !_servant.IsServant(args.User))
            return;

        // Fish-edit: CanReach до любых действий (в т.ч. поглощения листов).
        if (!args.CanReach)
            return;

        if (args.Target != null && TryComp<StackComponent>(args.Target.Value, out var stack))
        {
            if (TryConsumeSheets(ent, args.User, args.Target.Value, stack))
            {
                args.Handled = true;
                return;
            }
        }

        // Сначала стена; пол — только при клике по полу (без entity-target),
        // иначе отказ по неконвертируемой стене превращал бы пол под ней.
        if (args.Target != null)
        {
            if (TryConvertWall(ent, args.User, args.Target.Value))
                args.Handled = true;
            return;
        }

        if (TryConvertFloor(ent, args.User, args.ClickLocation))
            args.Handled = true;
    }

    private bool TryConsumeSheets(Entity<FishReplicaFabricatorComponent> fab, EntityUid user, EntityUid stackUid, StackComponent stack)
    {
        var id = MetaData(stackUid).EntityPrototype?.ID ?? string.Empty;
        var perSheet = ResolveSheetPower(id);
        if (perSheet <= 0f)
            return false;

        var amount = Math.Min(stack.Count, 10);
        if (amount <= 0)
            return false;

        var gained = amount * perSheet;
        if (!_stack.TryUse((stackUid, stack), amount))
            return false;

        // Fish-edit: только глобальный пул — без двойного учёта через StoredPower.
        _power.TryAdjustPower(gained, quiet: true);
        _popup.PopupEntity(Loc.GetString("fish-clockwork-fabricator-absorb", ("amount", amount)), user, user);
        return true;
    }

    private static float ResolveSheetPower(string protoId)
    {
        if (protoId.Contains("Plasteel", StringComparison.OrdinalIgnoreCase))
            return FishClockworkCultConstants.PowerPlasteel;
        if (protoId.Contains("Glass", StringComparison.OrdinalIgnoreCase) &&
            !protoId.Contains("Plass", StringComparison.OrdinalIgnoreCase))
            return FishClockworkCultConstants.PowerRod; // glass ≈ rod-tier
        if (protoId.Contains("Brass", StringComparison.OrdinalIgnoreCase) ||
            protoId.Contains("Steel", StringComparison.OrdinalIgnoreCase) ||
            protoId.Contains("Metal", StringComparison.OrdinalIgnoreCase))
            return FishClockworkCultConstants.PowerMetal;

        return 0f;
    }

    private bool TryConvertFloor(Entity<FishReplicaFabricatorComponent> fab, EntityUid user, EntityCoordinates coords)
    {
        if (!_turf.TryGetTileRef(coords, out var tile) || tile.Value.Tile.IsEmpty)
            return false;

        if (!_tiles.TryGetDefinition("FloorBrassFilled", out var brassTile))
            return false;

        // Fish-edit: не списывать мощность за уже латунный пол.
        if (tile.Value.Tile.TypeId == brassTile.TileId)
            return false;

        if (!_power.TrySpendPower(fab.Comp.FloorCost))
        {
            _popup.PopupEntity(Loc.GetString("fish-clockwork-not-enough-power"), user, user);
            return false;
        }

        var gridUid = tile.Value.GridUid;
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        _map.SetTile(gridUid, grid, tile.Value.GridIndices, new Tile(brassTile.TileId));
        _popup.PopupEntity(Loc.GetString("fish-clockwork-fabricator-floor"), user, user);
        return true;
    }

    private bool TryConvertWall(Entity<FishReplicaFabricatorComponent> fab, EntityUid user, EntityUid target)
    {
        var proto = MetaData(target).EntityPrototype?.ID ?? string.Empty;
        // Fish-edit: явный whitelist вместо StartsWith("Wall").
        if (!ConvertibleWalls.Contains(proto))
            return false;

        if (!_power.TrySpendPower(fab.Comp.WallCost))
        {
            _popup.PopupEntity(Loc.GetString("fish-clockwork-not-enough-power"), user, user);
            return false;
        }

        var coords = Transform(target).Coordinates;
        QueueDel(target);
        Spawn("WallBrass", coords);
        Spawn(SheetBrass, coords);
        _popup.PopupEntity(Loc.GetString("fish-clockwork-fabricator-wall"), user, user);
        return true;
    }
}
