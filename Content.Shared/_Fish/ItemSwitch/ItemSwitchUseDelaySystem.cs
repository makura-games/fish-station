using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Timing.Components;
using Content.Shared.Timing.Systems;

namespace Content.Shared._Fish.ItemSwitch;

/// <summary>
/// Интегрирует <see cref="ItemSwitchComponent"/> с <see cref="UseDelayComponent"/>.
/// </summary>
public sealed partial class ItemSwitchUseDelaySystem : EntitySystem
{
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private EntityQuery<UseDelayComponent> _useDelayQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemSwitchUseDelayComponent, ItemSwitchAttemptEvent>(OnSwitchAttempt);
        SubscribeLocalEvent<ItemSwitchUseDelayComponent, ItemSwitchedEvent>(OnSwitched);
    }

    private void OnSwitchAttempt(Entity<ItemSwitchUseDelayComponent> ent, ref ItemSwitchAttemptEvent args)
    {
        if (args.User == null)
            return;

        if (_useDelayQuery.TryComp(ent, out var delay) && _useDelay.IsDelayed((ent, delay)))
            args.Cancelled = true;
    }

    private void OnSwitched(Entity<ItemSwitchUseDelayComponent> ent, ref ItemSwitchedEvent args)
    {
        if (args.User == null)
            return;

        if (_useDelayQuery.TryComp(ent, out var delay))
            _useDelay.TryResetDelay((ent, delay));
    }
}
