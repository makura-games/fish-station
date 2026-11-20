using System.Numerics;
using Content.Shared._Fish.Homelander;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed partial class HitscanBasicRaycastSystem
{
    private (NetCoordinates Coordinates, Angle Angle, SpriteSpecifier Sprite, float Length, Color? Color, Vector2? Scale)
        GetOffsetTravelEffect(EntityUid shooter, EntityCoordinates origin, float distance, Angle shotAngle,
            Angle worldShotAngle, Vector2 offset, SpriteSpecifier sprite, Color? color, Vector2? scale)
    {
        if (TryComp<HomelanderFlightComponent>(shooter, out var flight) && flight.FlightEnabled)
            offset.Y += HomelanderComponent.FlightVisualLift;

        var start = (shotAngle - worldShotAngle).RotateVec(offset);
        var end = shotAngle.ToVec() * distance;
        var beam = end - start;
        var midpoint = origin.Offset((start + end) / 2);
        return (GetNetCoordinates(midpoint), beam.ToAngle(), sprite, beam.Length(), color, scale);
    }
}
