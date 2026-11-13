using Content.Shared._Fish.Stress;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Server.GameObjects;

namespace Content.Server._Fish.Stress;

/// <summary>
/// Применяет случайные импульсы к персонажу при наличии StressTremorComponent.
/// </summary>
public sealed partial class StressTremorSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PhysicsSystem _physics = default!;

    private float _accumulator;

    /// <summary>
    /// Обновляет эффекты дрожи для сущностей с высоким стрессом.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _accumulator += frameTime;

        var due = false;
        var query = EntityQueryEnumerator<StressTremorComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var tremor, out var physics))
        {
            if (_accumulator >= tremor.ImpulseInterval)
            {
                ApplyTremorImpulse(uid, tremor.Intensity, physics);
                due = true;
            }
        }

        if (due)
            _accumulator = 0f;
    }

    /// <summary>
    /// Применяет небольшое случайное смещение к физической скорости сущности.
    /// </summary>
    private void ApplyTremorImpulse(EntityUid uid, float intensity, PhysicsComponent physics)
    {
        if (physics.BodyType != BodyType.Dynamic)
            return;

        var randomAngle = _random.NextAngle();
        var force = _random.NextFloat(0.5f, 1f) * intensity;
        var impulse = randomAngle.ToVec() * force;

        _physics.ApplyLinearImpulse(uid, impulse, body: physics);
        _physics.ApplyAngularImpulse(uid, _random.NextFloat(-0.1f, 0.1f) * intensity, body: physics);
    }
}
