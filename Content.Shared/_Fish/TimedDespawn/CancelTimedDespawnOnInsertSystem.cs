using Content.Shared.Hands;
using Robust.Shared.Containers;
using Robust.Shared.Spawners;

namespace Content.Shared._Fish.TimedDespawn;

/// <summary>
/// Общий механизм отмены TimedDespawn при подборе предмета.
/// </summary>
public sealed class CancelTimedDespawnOnInsertSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CancelTimedDespawnOnInsertComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CancelTimedDespawnOnInsertComponent, GotEquippedHandEvent>(OnEquippedHand);
        SubscribeLocalEvent<CancelTimedDespawnOnInsertComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
    }

    private void OnMapInit(Entity<CancelTimedDespawnOnInsertComponent> ent, ref MapInitEvent args)
    {
        // Уже в контейнере при спавне (loadout / StorageFill) — таймер не нужен.
        if (_container.IsEntityInContainer(ent.Owner))
            Cancel(ent.Owner);
    }

    private void OnEquippedHand(Entity<CancelTimedDespawnOnInsertComponent> ent, ref GotEquippedHandEvent args)
    {
        Cancel(ent.Owner);
    }

    private void OnInserted(Entity<CancelTimedDespawnOnInsertComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        Cancel(ent.Owner);
    }

    private void Cancel(EntityUid uid)
    {
        RemCompDeferred<TimedDespawnComponent>(uid);
        RemCompDeferred<CancelTimedDespawnOnInsertComponent>(uid);
    }
}
