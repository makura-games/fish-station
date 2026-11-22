using Content.Shared.Chemistry.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Effects;

[Serializable, NetSerializable]
public enum FishParticleKind : byte
{
    BloodSpray,
    BloodDrip,
    Weld,
    Slag,
    Splash,
}

[Serializable, NetSerializable]
public sealed class FishParticleBurstEvent(NetCoordinates position, FishParticleKind kind, Color color) : EntityEventArgs
{
    public readonly NetCoordinates Position = position;
    public readonly FishParticleKind Kind = kind;
    public readonly Color Color = color;
}

public sealed partial class FishActionParticleSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public void Emit(EntityUid source, FishParticleKind kind, Solution? liquid = null)
    {
        if (!_net.IsServer || liquid?.Volume == 0 || _containers.IsEntityOrParentInContainer(source))
            return;

        var xform = Transform(source);
        if (xform.MapUid is not {} map)
            return;

        var position = _transform.ToCoordinates(xform.GridUid ?? map, _transform.GetMapCoordinates(source));
        var color = liquid?.GetColor(ProtoMan) ?? Color.White;
        RaiseNetworkEvent(new FishParticleBurstEvent(GetNetCoordinates(position), kind, color),
            Filter.Pvs(source, entityManager: EntityManager));
    }
}
