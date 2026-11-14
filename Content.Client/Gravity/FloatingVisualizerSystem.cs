using System.Numerics;
using Content.Client._Fish.Homelander; // Fish-edit
using Content.Shared._Fish.Homelander; // Fish-edit
using Content.Shared._Sunrise.Abilities.Milira; // Fish-edit
using Content.Shared.Gravity;
using Robust.Client.GameObjects;
using Robust.Client.Animations;
using Robust.Shared.Animations;

namespace Content.Client.Gravity;

/// <inheritdoc/>
public sealed partial class FloatingVisualizerSystem : SharedFloatingVisualizerSystem
{
    [Dependency] private AnimationPlayerSystem AnimationSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FloatingVisualsComponent, AnimationCompletedEvent>(OnAnimationCompleted);
    }

    /// <inheritdoc/>
    public override void FloatAnimation(EntityUid uid, Vector2 offset, string animationKey, float animationTime, bool stop = false)
    {
        if (stop)
        {
            AnimationSystem.Stop(uid, animationKey);
            return;
        }

        // Fish-start
        if (HasComp<HomelanderLiftComponent>(uid) ||
            (HasComp<HomelanderComponent>(uid) &&
                TryComp<WingFlightComponent>(uid, out var flight) && flight.FlightEnabled))
        {
            return;
        }
        // Fish-end

        var animation = new Animation
        {
            // We multiply by the number of extra keyframes to make time for them
            Length = TimeSpan.FromSeconds(animationTime*2),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(offset, animationTime),
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, animationTime),
                    }
                }
            }
        };

        if (!AnimationSystem.HasRunningAnimation(uid, animationKey))
            AnimationSystem.Play(uid, animation, animationKey);
    }

    // Fish-start
    internal void RefreshHomelanderBobbing(EntityUid uid, bool flying)
    {
        if (!TryComp<FloatingVisualsComponent>(uid, out var floating))
            return;

        AnimationSystem.Stop(uid, floating.AnimationKey);
        if (!flying && floating.CanFloat && !TerminatingOrDeleted(uid))
            FloatAnimation(uid, floating.Offset, floating.AnimationKey, floating.AnimationTime);
    }
    // Fish-end

    private void OnAnimationCompleted(EntityUid uid, FloatingVisualsComponent component, AnimationCompletedEvent args)
    {
        if (args.Key != component.AnimationKey)
            return;

        FloatAnimation(uid, component.Offset, component.AnimationKey, component.AnimationTime, stop: !component.CanFloat);
    }
}
