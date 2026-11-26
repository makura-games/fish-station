using Content.Shared.Movement.Components;
using Robust.Shared.Map.Components;

namespace Content.Shared.Movement.Systems;

public abstract partial class SharedMoverController
{
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _relativeMovers = new();

    private void InitializeGridWeldingMovement()
    {
        SubscribeLocalEvent<InputMoverComponent, ComponentShutdown>(OnRelativeMoverShutdown);
        SubscribeLocalEvent<MapGridComponent, EntityTerminatingEvent>(OnRelativeGridTerminating);
    }

    /// <summary>Обновляет опору движения и список наблюдателей, пока старый грид ещё существует.</summary>
    public void SetRelativeEntity(Entity<InputMoverComponent> mover, EntityUid? relative)
    {
        if (mover.Comp.RelativeEntity == relative)
            return;

        if (mover.Comp.RelativeEntity is {} old && _relativeMovers.TryGetValue(old, out var previous))
        {
            previous.Remove(mover.Owner);
            if (previous.Count == 0)
                _relativeMovers.Remove(old);
        }

        mover.Comp.RelativeEntity = relative;
        if (relative is not {} next)
            return;

        if (!_relativeMovers.TryGetValue(next, out var movers))
            _relativeMovers[next] = movers = new();
        movers.Add(mover.Owner);
    }

    private void OnRelativeMoverShutdown(Entity<InputMoverComponent> ent, ref ComponentShutdown args)
    {
        SetRelativeEntity(ent, null);
    }

    private void OnRelativeGridTerminating(Entity<MapGridComponent> ent, ref EntityTerminatingEvent args)
    {
        // Забираем набор до смены ссылок, чтобы не менять коллекцию во время обхода.
        if (!_relativeMovers.Remove(ent.Owner, out var movers))
            return;

        foreach (var uid in movers)
        {
            if (!TerminatingOrDeleted(uid) && MoverQuery.TryComp(uid, out var mover))
                TryUpdateRelative(uid, mover, Transform(uid));
        }
    }
}
