using Content.Shared.Actions;
using Content.Shared.Clothing.Components;
using Content.Shared.Foldable;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Fish.Clothing;

/// <summary>
/// Keeps the visor fold state synchronized with the standard clothing toggle action.
/// </summary>
public sealed partial class VisorToggleSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private FoldableSystem _foldable = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VisorComponent, ItemToggleActivateAttemptEvent>(OnActivateAttempt);
        SubscribeLocalEvent<VisorComponent, ItemToggleDeactivateAttemptEvent>(OnDeactivateAttempt);
        SubscribeLocalEvent<VisorComponent, ItemToggledEvent>(OnItemToggled);
        SubscribeLocalEvent<VisorComponent, FoldedEvent>(OnFolded);
    }

    private void OnActivateAttempt(Entity<VisorComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (!CanSetClosed(ent!, true))
            args.Cancelled = true;
    }

    private void OnDeactivateAttempt(Entity<VisorComponent> ent, ref ItemToggleDeactivateAttemptEvent args)
    {
        if (!CanSetClosed(ent!, false))
            args.Cancelled = true;
    }

    private void OnItemToggled(Entity<VisorComponent> ent, ref ItemToggledEvent args)
    {
        TrySetClosed(ent!, args.Activated, args.User);
    }

    private void OnFolded(Entity<VisorComponent> ent, ref FoldedEvent args)
    {
        var visorEvent = new VisorToggledEvent(!args.IsFolded);
        RaiseLocalEvent(ent, ref visorEvent);

        if (_timing.ApplyingState)
            return;

        var closed = !args.IsFolded;

        if (TryComp<ItemToggleComponent>(ent, out var toggle) && toggle.Activated != closed)
            _itemToggle.TrySetActive((ent, toggle), closed, args.User, showPopup: false);

        if (TryComp<ToggleClothingComponent>(ent, out var clothingToggle) &&
            clothingToggle.ActionEntity is { } action)
        {
            _actions.SetToggled(action, closed);
        }
    }

    public bool TrySetClosed(Entity<VisorComponent?> ent, bool closed, EntityUid? user = null)
    {
        if (!CanSetClosed(ent, closed) || !TryComp<FoldableComponent>(ent, out var foldable))
            return false;

        var folded = !closed;
        if (foldable.IsFolded == folded)
            return true;

        _foldable.SetFolded(ent, foldable, folded, user);
        return true;
    }

    public bool CanSetClosed(Entity<VisorComponent?> ent, bool closed)
    {
        if (!Resolve(ent, ref ent.Comp) || !TryComp<FoldableComponent>(ent, out var foldable))
            return false;

        var folded = !closed;
        return foldable.IsFolded == folded || _foldable.CanToggleFold(ent, foldable);
    }
}

/// <summary>
/// Raised after the visor fold state changes.
/// </summary>
[ByRefEvent]
public readonly record struct VisorToggledEvent(bool Closed);
