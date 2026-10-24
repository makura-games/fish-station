using Content.Server.Popups;
using Content.Shared._Fish.GridWelding;
using Content.Shared.ActionBlocker;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Fish.GridWelding;

/// <summary>
/// Welds two adjacent, aligned grid edges together without stopping their shared flight.
/// </summary>
public sealed partial class GridWeldingSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private IMapManager _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private ILocalizationManager _loc = default!;

    private const string WeldingQuality = "Welding";
    private const float WeldingTime = 5f;
    private const float WeldingFuel = 2f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
        SubscribeLocalEvent<WelderComponent, ComponentStartup>(OnWelderStartup);
        SubscribeLocalEvent<GridWeldingComponent, BeforeRangedInteractEvent>(OnBeforeInteract);
        SubscribeLocalEvent<GridWeldingComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<GridWeldingComponent, GridWeldingDoAfterEvent>(OnWeldFinished);
        SubscribeLocalEvent<GridWeldingComponent, DoAfterAttemptEvent<GridWeldingDoAfterEvent>>(OnWeldAttempt);
    }

    private void OnWelderStartup(Entity<WelderComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<GridWeldingComponent>(ent);
    }

    private void OnBeforeInteract(Entity<GridWeldingComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled || !CanSelectTarget(ent, args.User, args.Target) ||
            !TryComp(ent, out WelderComponent? welder))
            return;

        args.Handled = TrySelectTile((ent, welder), args.User, args.ClickLocation, args.Target);
    }

    /// <summary>
    /// Selects the first tile or starts welding to another grid, resolving overlapping grids at the click position.
    /// Clicking the selected tile again clears the selection.
    /// </summary>
    public bool TrySelectTile(Entity<WelderComponent> welder, EntityUid user, EntityCoordinates location,
        EntityUid? target = null)
    {
        if (!CanUseWelder(welder.AsNullable(), user))
            return false;

        var selection = EnsureComp<GridWeldingComponent>(welder);
        if (selection.DoAfter != null)
            return true;

        var selectedGridUid = selection.User == user ? (EntityUid?) selection.Grid : null;
        if (!TryGetClickedGrid(location, target, selectedGridUid, out var clickedGrid))
            return false;

        var (gridUid, grid) = clickedGrid;
        var tile = _maps.GetTileRef(gridUid, grid, location);
        if (!CanReachTile(user, gridUid, grid, tile.GridIndices))
        {
            Popup(user, "fish-grid-welding-unreachable");
            return true;
        }

        if (selection.User == user && selection.Grid == gridUid && selection.Tile == tile.GridIndices)
        {
            ClearSelection(selection);
            Popup(user, "fish-grid-welding-cleared");
            return true;
        }

        if (selection.User != user ||
            !TryComp(selection.Grid, out MapGridComponent? selectedGrid) ||
            _maps.GetTileRef(selection.Grid, selectedGrid, selection.Tile).Tile.IsEmpty)
        {
            if (!IsEdgeTile(gridUid, grid, tile.GridIndices))
            {
                Popup(user, "fish-grid-welding-edge-required");
                return true;
            }

            selection.User = user;
            selection.Grid = gridUid;
            selection.Tile = tile.GridIndices;
            Popup(user, "fish-grid-welding-selected");
            return true;
        }

        if (selection.Grid == gridUid)
        {
            Popup(user, "fish-grid-welding-same-grid");
            return true;
        }

        return TryStartWelding((welder, selection), user, gridUid, tile.GridIndices);
    }

    /// <summary>
    /// Starts a server-validated welding action between the selected tile and another grid's edge.
    /// </summary>
    public bool TryStartWelding(Entity<GridWeldingComponent> welder, EntityUid user, EntityUid second, Vector2i secondTile)
    {
        var ev = new GridWeldingDoAfterEvent(GetNetEntity(welder.Comp.Grid), GetNetEntity(second), welder.Comp.Tile, secondTile);
        if (!CanWeld(welder, user, ev, out var alignment, out var reason) ||
            !CanMergeGrids(welder.Comp.Grid, second, alignment, out reason))
        {
            Popup(user, reason);
            return true;
        }

        var userAttempt = new ToolUserAttemptUseEvent(null);
        RaiseLocalEvent(user, ref userAttempt);
        var toolAttempt = new ToolUseAttemptEvent(user, WeldingFuel);
        RaiseLocalEvent(welder, toolAttempt);
        if (userAttempt.Cancelled || toolAttempt.Cancelled)
            return true;

        var tool = Comp<ToolComponent>(welder);
        if (!float.IsFinite(tool.SpeedModifier) || tool.SpeedModifier <= 0)
            return false;

        var args = new DoAfterArgs(EntityManager, user, WeldingTime / tool.SpeedModifier, ev, welder, used: welder)
        {
            NeedHand = true,
            BreakOnDamage = true,
            // Координаты тайлов проверяются отдельно: общий полёт гридов не должен отменять сварку.
            BreakOnMove = false,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };

        if (_doAfter.TryStartDoAfter(args, out var id))
        {
            welder.Comp.DoAfter = id;
            Popup(user, "fish-grid-welding-started");
        }
        return true;
    }

    private void OnWeldAttempt(Entity<GridWeldingComponent> ent, ref DoAfterAttemptEvent<GridWeldingDoAfterEvent> args)
    {
        if (!CanWeld(ent, args.DoAfter.Args.User, args.Event, out _, out _))
            args.Cancel();
    }

    private void OnWeldFinished(Entity<GridWeldingComponent> ent, ref GridWeldingDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.DoAfter = null;
        if (args.Cancelled)
        {
            Popup(args.User, "fish-grid-welding-interrupted");
            return;
        }

        TryFinishWelding(ent, args);
    }

    private bool TryFinishWelding(Entity<GridWeldingComponent> welder, GridWeldingDoAfterEvent args)
    {
        var first = GetEntity(args.FirstGrid);
        var second = GetEntity(args.SecondGrid);
        if (!CanWeld(welder, args.User, args, out var alignment, out var reason) ||
            !CanMergeGrids(first, second, alignment, out reason))
        {
            Popup(args.User, reason);
            return false;
        }

        var comp = Comp<WelderComponent>(welder);
        if (!_solutionContainer.TryGetSolution(welder.Owner, comp.FuelSolutionName, out var solution))
            return false;

        _solutionContainer.RemoveReagent(solution.Value, comp.FuelReagent, FixedPoint2.New(WeldingFuel));
        _tool.PlayToolSound(welder, Comp<ToolComponent>(welder), args.User);
        MergeGrids(first, second, alignment, args.User);
        ClearSelection(welder.Comp);
        Popup(args.User, "fish-grid-welding-complete");
        return true;
    }

    private void OnUnequipped(Entity<GridWeldingComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (ent.Comp.DoAfter is { } id)
            _doAfter.Cancel(id);
        ClearSelection(ent.Comp);
    }

    private static void ClearSelection(GridWeldingComponent comp)
    {
        comp.User = default;
        comp.Grid = default;
        comp.Tile = default;
        comp.DoAfter = null;
    }

    private bool CanUseWelder(Entity<WelderComponent?> welder, EntityUid user)
    {
        return !TerminatingOrDeleted(welder) && Resolve(welder, ref welder.Comp, false) && !TerminatingOrDeleted(user) &&
               _hands.GetActiveItem(user) == welder.Owner && _actionBlocker.CanComplexInteract(user) &&
               _itemToggle.IsActivated(welder.Owner) && _tool.HasQuality(welder, WeldingQuality) &&
               _tool.GetWelderFuelAndCapacity(welder, welder.Comp).fuel >= FixedPoint2.New(WeldingFuel);
    }

    private bool IsEdgeTile(EntityUid uid, MapGridComponent grid, Vector2i tile)
    {
        return _maps.GetTileRef(uid, grid, tile + Vector2i.Up).Tile.IsEmpty ||
               _maps.GetTileRef(uid, grid, tile + Vector2i.Down).Tile.IsEmpty ||
               _maps.GetTileRef(uid, grid, tile + Vector2i.Left).Tile.IsEmpty ||
               _maps.GetTileRef(uid, grid, tile + Vector2i.Right).Tile.IsEmpty;
    }

    private void Popup(EntityUid user, string key)
    {
        _popup.PopupEntity(_loc.GetString(key), user, user);
    }
}
