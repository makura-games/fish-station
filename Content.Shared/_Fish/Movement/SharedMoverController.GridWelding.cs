using Content.Shared.Movement.Components;
using Robust.Shared.Map.Components;

namespace Content.Shared.Movement.Systems;

public abstract partial class SharedMoverController
{
    private void InitializeGridWeldingMovement()
    {
        SubscribeLocalEvent<MapGridComponent, EntityTerminatingEvent>(OnRelativeGridTerminating);
    }

    /// <summary>Refreshes delayed movement references while the departing grid's transform is still available.</summary>
    private void OnRelativeGridTerminating(Entity<MapGridComponent> ent, ref EntityTerminatingEvent args)
    {
        // После слияния родитель уже сменился, но задержка камеры ещё удерживает старый грид.
        // Включаем неподвижных и приостановленных персонажей: их состояние тоже может передаваться клиенту.
        var query = AllEntityQuery<InputMoverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mover, out var xform))
        {
            if (mover.RelativeEntity != ent.Owner || TerminatingOrDeleted(uid))
                continue;

            TryUpdateRelative(uid, mover, xform);
        }
    }
}
