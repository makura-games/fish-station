using Content.Server.Singularity.Components;
using Content.Server.Singularity.EntitySystems;
using Content.Shared.Anomaly.Effects.Components;

namespace Content.Server.Anomaly.Effects;

public sealed partial class GravityAnomalySystem
{
    [Dependency] private EntityQuery<GravityWellComponent> _gravityWells = default!;
    private float _fieldUpdateDelay;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _fieldUpdateDelay -= frameTime;
        if (_fieldUpdateDelay > 0f)
            return;

        _fieldUpdateDelay = 0.25f;
        var anomalies = EntityQueryEnumerator<GravityAnomalyComponent>();
        while (anomalies.MoveNext(out var uid, out var anomaly))
        {
            // Берём параметры самой силы, в том числе переопределённые картой или администратором.
            var field = _gravityWells.TryComp(uid, out var well)
                ? new GravityAnomalyField(MathF.Sqrt(Math.Max(well.MinRange * well.MinRange, GravityWellSystem.MinGravPulseRange)),
                    well.MaxRange, well.BaseRadialAcceleration, well.BaseTangentialAcceleration)
                : default;
            if (field == anomaly.Field)
                continue;

            anomaly.Field = field;
            Dirty(uid, anomaly);
        }
    }
}
