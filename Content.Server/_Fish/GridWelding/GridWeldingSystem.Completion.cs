using Content.Shared._Fish.GridWelding;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Tools.Components;
using Robust.Shared.Timing;

namespace Content.Server._Fish.GridWelding;

public sealed partial class GridWeldingSystem
{
    // Слияние геометрии атомарно: распределяем готовые действия по тикам без правок движка.
    private readonly Queue<(EntityUid Welder, GridWeldingDoAfterEvent Action)> _pendingWelds = new();
    private GameTick _lastWeldCompletionTick = GameTick.MaxValue;

    public override void Shutdown()
    {
        ClearPendingWelds();
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_pendingWelds.Count == 0 || _lastWeldCompletionTick == _timing.CurTick)
            return;

        var remaining = _pendingWelds.Count;
        while (remaining-- > 0 && _pendingWelds.TryDequeue(out var request))
        {
            if (TerminatingOrDeleted(request.Welder) ||
                !TryComp(request.Welder, out GridWeldingComponent? selection) ||
                !ReferenceEquals(selection.PendingWeld, request.Action))
                continue;

            if (IsPendingWeldPaused(request.Welder, request.Action))
            {
                _pendingWelds.Enqueue(request);
                continue;
            }

            // Старые записи после сброса выбора не должны выполнить или очистить новое действие.
            selection.PendingWeld = null;
            _lastWeldCompletionTick = _timing.CurTick;
            TryFinishWelding((request.Welder, selection), request.Action);
            return;
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        ClearPendingWelds();
    }

    /// <summary>Queues only a completed action while releasing its active DoAfter tracking.</summary>
    private bool TryQueueCompletedWeld(Entity<GridWeldingComponent> welder, GridWeldingDoAfterEvent args)
    {
        var canQueue = CanQueueCompletedWeld(welder, args);
        welder.Comp.DoAfter = null;
        if (!canQueue)
        {
            if (args.Cancelled)
                Popup(args.User, "fish-grid-welding-interrupted");
            return false;
        }

        welder.Comp.PendingWeld = args;
        _pendingWelds.Enqueue((welder, args));
        return true;
    }

    private bool CanQueueCompletedWeld(Entity<GridWeldingComponent> welder, GridWeldingDoAfterEvent args)
    {
        return !args.Cancelled && !TerminatingOrDeleted(welder) && welder.Comp.PendingWeld == null;
    }

    /// <summary>Rechecks the queued action before spending fuel and transferring either grid's contents.</summary>
    private bool TryFinishWelding(Entity<GridWeldingComponent> welder, GridWeldingDoAfterEvent args)
    {
        var first = GetEntity(args.FirstGrid);
        var second = GetEntity(args.SecondGrid);
        if (!CanFinishWelding(welder, args, out var alignment, out var reason))
        {
            if (!TerminatingOrDeleted(args.User))
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

    /// <summary>Checks interaction permission again after the queue wait, before any expensive merge validation.</summary>
    private bool CanFinishWelding(Entity<GridWeldingComponent> welder, GridWeldingDoAfterEvent args,
        out WeldAlignment alignment, out string reason)
    {
        if (!CanWeld(welder, args.User, args, out alignment, out reason))
            return false;

        reason = "fish-grid-welding-unavailable";
        if (!_actionBlocker.CanInteract(args.User, args.Target))
            return false;

        return CanMergeGrids(GetEntity(args.FirstGrid), GetEntity(args.SecondGrid), alignment, out reason);
    }

    /// <summary>Retains paused requests without blocking requests on other maps.</summary>
    private bool IsPendingWeldPaused(EntityUid welder, GridWeldingDoAfterEvent args)
    {
        var first = GetEntity(args.FirstGrid);
        var second = GetEntity(args.SecondGrid);
        if (TerminatingOrDeleted(args.User) || TerminatingOrDeleted(first) || TerminatingOrDeleted(second))
            return false;

        return Paused(welder) ||
               Paused(args.User) || Paused(first) || Paused(second);
    }

    private void ClearPendingWelds()
    {
        while (_pendingWelds.TryDequeue(out var request))
        {
            if (TryComp(request.Welder, out GridWeldingComponent? selection) &&
                ReferenceEquals(selection.PendingWeld, request.Action))
                ClearSelection(selection);
        }
    }
}
