using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Events;

namespace Content.Shared.Holopad;

public sealed partial class RemoteViewerFrozenSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RemoteViewerFrozenComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<RemoteViewerFrozenComponent, ComponentShutdown>(OnFrozenShutdown);
    }

    private void OnUpdateCanMove(Entity<RemoteViewerFrozenComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        args.Cancel();
    }

    private void OnFrozenShutdown(Entity<RemoteViewerFrozenComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return;

        _blocker.UpdateCanMove(ent.Owner);
    }
}
