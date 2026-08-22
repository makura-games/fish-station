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

    private void OnMapInit(EntityUid uid, CancelTimedDespawnOnInsertComponent component, MapInitEvent args)
    {
        // Уже в контейнере при спавне (loadout / StorageFill) — таймер не нужен.
        if (_container.IsEntityInContainer(uid))
            Cancel(uid);
    }

    private void OnEquippedHand(EntityUid uid, CancelTimedDespawnOnInsertComponent component, GotEquippedHandEvent args)
    {
        Cancel(uid);
    }

    private void OnInserted(EntityUid uid, CancelTimedDespawnOnInsertComponent component, EntGotInsertedIntoContainerMessage args)
    {
        Cancel(uid);
    }

    private void Cancel(EntityUid uid)
    {
        RemCompDeferred<TimedDespawnComponent>(uid);
        RemCompDeferred<CancelTimedDespawnOnInsertComponent>(uid);
    }
}
