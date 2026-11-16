using Content.Shared.Atmos.Components;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Atmos.EntitySystems;

public sealed partial class FlammableSystem
{
    private readonly List<Entity<FlammableComponent>> _fishUpdateEntities = new();

    private void CollectFlammableUpdateEntities(TimeSpan curTime)
    {
        _fishUpdateEntities.Clear();

        var query = EntityQueryEnumerator<FlammableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var flammable, out _))
        {
            if (curTime < flammable.NextUpdate)
                continue;

            _fishUpdateEntities.Add((uid, flammable));
        }
    }
}
