using Content.Server.Popups;
using Content.Shared._Fish.GridWelding;
using Content.Shared.ActionBlocker;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
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
        SubscribeLocalEvent<GridWeldingComponent, ComponentShutdown>(OnWeldingShutdown);
        SubscribeLocalEvent<GridWeldingComponent, BeforeRangedInteractEvent>(OnBeforeInteract);
        SubscribeLocalEvent<GridWeldingComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<GridWeldingComponent, GridWeldingDoAfterEvent>(OnWeldFinished);
        SubscribeLocalEvent<GridWeldingComponent, DoAfterAttemptEvent<GridWeldingDoAfterEvent>>(OnWeldAttempt);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnWelderStartup(Entity<WelderComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<GridWeldingComponent>(ent);
    }

    private void OnWeldingShutdown(Entity<GridWeldingComponent> ent, ref ComponentShutdown args)
    {
        if (_doAfter.IsRunning(ent.Comp.DoAfter))
            _doAfter.Cancel(ent.Comp.DoAfter);
        ClearSelection(ent.Comp);
    }

    private void OnBeforeInteract(Entity<GridWeldingComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TrySelectTile((ent.Owner, null), args.User, args.ClickLocation, args.Target);
    }

    /// <summary>Revalidates the moving seam every tick so changed geometry or lost tool access cancels the action.</summary>
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
        TryQueueCompletedWeld(ent, args);
    }

    /// <summary>Releases the selected seam and interrupts active or queued welding when the user drops the tool.</summary>
    private void OnUnequipped(Entity<GridWeldingComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (_doAfter.IsRunning(ent.Comp.DoAfter))
            _doAfter.Cancel(ent.Comp.DoAfter);
        else if (ent.Comp.PendingWeld != null)
            Popup(args.User, "fish-grid-welding-interrupted");
        ClearSelection(ent.Comp);
    }

    /// <summary>
    /// Selects the first tile or starts welding to another grid, resolving overlapping grids at the click position.
    /// Clicking the selected tile again clears the selection.
    /// </summary>
    /// <returns>Whether welding consumed the click, including rejected grid clicks that displayed feedback.</returns>
    public bool TrySelectTile(Entity<WelderComponent?> welder, EntityUid user, EntityCoordinates location,
        EntityUid? target = null)
    {
        if (!CanSelectTile(welder, user, location, target, out var grid, out var tile, out var handled))
            return handled;

        var selection = EnsureComp<GridWeldingComponent>(welder);
        return SelectTile((welder, selection), user, grid, tile);
    }

    /// <summary>
    /// Starts a server-validated welding action between the selected tile and another grid's edge.
    /// </summary>
    public bool TryStartWelding(Entity<GridWeldingComponent> welder, EntityUid user, EntityUid second, Vector2i secondTile)
    {
        if (welder.Comp.DoAfter != null || welder.Comp.PendingWeld != null)
            return true;

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

        if (_doAfter.TryStartDoAfter(args, out var id) && _doAfter.IsRunning(id))
        {
            welder.Comp.DoAfter = id;
            Popup(user, "fish-grid-welding-started");
        }
        return true;
    }

    private static void ClearSelection(GridWeldingComponent comp)
    {
        comp.User = default;
        comp.Grid = default;
        comp.Tile = default;
        comp.DoAfter = null;
        comp.PendingWeld = null;
    }

    /// <summary>Checks active-hand ownership, interaction permission, tool activation and fuel without consuming any.</summary>
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
