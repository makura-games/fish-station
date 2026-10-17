using Content.Shared._Fish.Vision;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Temperature.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Vision;

/// <summary>Samples nearby entity temperatures without sending contacts or expanding visibility.</summary>
public sealed partial class FishThermalVisionSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityQuery<FishThermalVisionComponent> _visionQuery = default!;
    [Dependency] private EntityQuery<EyeProtectionComponent> _eyeProtectionQuery = default!;

    private readonly HashSet<Entity<TemperatureComponent>> _candidates = new();
    private readonly HashSet<EntityUid> _sampled = new();
    private TimeSpan _nextSample;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TemperatureComponent, ComponentShutdown>(OnTemperatureShutdown);
        SubscribeLocalEvent<FishThermalSignatureComponent, ComponentGetStateAttemptEvent>(OnGetStateAttempt);
    }

    private void OnTemperatureShutdown(Entity<TemperatureComponent> ent, ref ComponentShutdown args)
    {
        RemCompDeferred<FishThermalSignatureComponent>(ent);
    }

    private void OnGetStateAttempt(Entity<FishThermalSignatureComponent> ent, ref ComponentGetStateAttemptEvent args)
    {
        if (args.Player != null && !CanReceiveTemperature(ent, args.Player))
            args.Cancelled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextSample)
            return;

        _nextSample = _timing.CurTime + TimeSpan.FromSeconds(0.25);
        _sampled.Clear();
        var viewers = EntityQueryEnumerator<FishThermalVisionComponent, ActorComponent>();
        while (viewers.MoveNext(out var uid, out var vision, out var actor))
        {
            if (actor.PlayerSession.AttachedEntity != uid || _eyeProtectionQuery.HasComp(uid) ||
                !float.IsFinite(vision.Range) || vision.Range <= 0f)
                continue;

            _candidates.Clear();
            _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(uid), Math.Min(vision.Range, 32f),
                _candidates, LookupFlags.Uncontained);
            foreach (var candidate in _candidates)
            {
                if (!_sampled.Add(candidate) || _container.IsEntityInContainer(candidate) ||
                    TerminatingOrDeleted(candidate))
                    continue;

                var value = candidate.Comp.CurrentTemperature;
                if (!float.IsFinite(value) || value < 0f)
                {
                    RemCompDeferred<FishThermalSignatureComponent>(candidate);
                    continue;
                }

                // PVS ограничивает видимость сущности; право получать температуру проверяется отдельно.
                var signature = EnsureComp<FishThermalSignatureComponent>(candidate);
                value = MathF.Round(value);
                signature.Temperature = value;
                // Неизменная температура тоже нужна зрителю, который только что включил термалы или подошёл.
                Dirty(candidate, signature);
            }
        }

        var signatures = EntityQueryEnumerator<FishThermalSignatureComponent>();
        while (signatures.MoveNext(out var uid, out _))
        {
            if (!_sampled.Contains(uid))
                RemCompDeferred<FishThermalSignatureComponent>(uid);
        }
    }

    /// <summary>Checks live recipient eligibility independently of the target's PVS visibility.</summary>
    private bool CanReceiveTemperature(EntityUid target, ICommonSession session)
    {
        if (session.AttachedEntity is not { } viewer || TerminatingOrDeleted(viewer) ||
            !_visionQuery.TryComp(viewer, out var vision) || _eyeProtectionQuery.HasComp(viewer) ||
            !float.IsFinite(vision.Range) || vision.Range <= 0f || _container.IsEntityInContainer(target))
            return false;

        var origin = _transform.GetMapCoordinates(viewer);
        var contact = _transform.GetMapCoordinates(target);
        var range = Math.Min(vision.Range, 32f);
        return origin.MapId == contact.MapId && (origin.Position - contact.Position).LengthSquared() <= range * range;
    }
}
