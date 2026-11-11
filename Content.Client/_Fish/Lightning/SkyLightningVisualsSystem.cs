using System.Numerics;
using Content.Shared._Fish.Lightning;
using Robust.Client.GameObjects;
using Robust.Client.Animations;
using Robust.Shared.Animations;
using Robust.Client.ComponentTrees;

namespace Content.Client._Fish.Lightning;

public sealed partial class SkyLightningVisualsSystem : EntitySystem
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private SpriteTreeSystem _spriteTree = default!;

    private const string AnimationKey = "sky-lightning";
    private HashSet<EntityUid> _pending = new();
    private HashSet<EntityUid> _active = new();
    private List<EntityUid> _finished = new();

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(AppearanceSystem));
        UpdatesAfter.Add(typeof(AnimationPlayerSystem));
        UpdatesBefore.Add(typeof(SpriteTreeSystem));
        SubscribeLocalEvent<SkyLightningVisualsComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    public override void Shutdown()
    {
        _pending.Clear();
        _active.Clear();
        _finished.Clear();
        base.Shutdown();
    }

    private void OnAppearanceChange(Entity<SkyLightningVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        if (!ent.Comp.Played)
            _pending.Add(ent.Owner);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        // Запускаем после обновления анимаций: прошедший длинный кадр не должен съесть новый разряд.
        foreach (var uid in _pending)
        {
            if (!TerminatingOrDeleted(uid) && TryComp<SkyLightningVisualsComponent>(uid, out var comp))
                TryPlay((uid, comp));
        }

        _pending.Clear();

        foreach (var uid in _active)
        {
            if (TerminatingOrDeleted(uid) || !TryComp<SpriteComponent>(uid, out var sprite))
            {
                _finished.Add(uid);
                continue;
            }

            // Scale и Offset не обновляют дерево отрисовки автоматически в этой версии движка.
            _spriteTree.QueueTreeUpdate((uid, sprite));
            if (!_animation.HasRunningAnimation(uid, AnimationKey))
                _finished.Add(uid);
        }

        foreach (var uid in _finished)
        {
            _active.Remove(uid);
        }

        _finished.Clear();
    }

    private void TryPlay(Entity<SkyLightningVisualsComponent> ent)
    {
        if (ent.Comp.Played || !TryComp<SpriteComponent>(ent, out var sprite) ||
            !_appearance.TryGetData<float>(ent, SkyLightningVisuals.Tilt, out var tilt) ||
            !_appearance.TryGetData<int>(ent, SkyLightningVisuals.Variant, out var variant))
            return;

        ent.Comp.Played = true;
        var angle = Angle.FromDegrees(tilt);
        var fullScale = new Vector2(sprite.Scale.X, ent.Comp.HalfHeight * 2);
        var startScale = new Vector2(fullScale.X, 0.01f);
        var startOffset = angle.RotateVec(new Vector2(0, ent.Comp.HalfHeight * 2 - 0.005f));
        var endOffset = angle.RotateVec(new Vector2(0, ent.Comp.HalfHeight));
        var reveal = ent.Comp.RevealDuration;

        // NoRotation сохраняет направление с неба даже при повороте камеры или грида.
        _sprite.SetRotation((ent, sprite), angle);
        _sprite.LayerSetRsiState((ent, sprite), SkyLightningLayers.Bolt, $"lightning_{variant}");
        _sprite.SetScale((ent, sprite), startScale);
        _sprite.SetOffset((ent, sprite), startOffset);
        _sprite.SetVisible((ent, sprite), true);

        // Верхний конец неподвижен, нижний достигает точки удара к концу раскрытия.
        var animation = new Animation
        {
            Length = TimeSpan.FromSeconds(reveal + 0.43f),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startScale, 0),
                        new AnimationTrackProperty.KeyFrame(fullScale, reveal),
                    },
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startOffset, 0),
                        new AnimationTrackProperty.KeyFrame(endOffset, reveal),
                    },
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Color.White, 0),
                        new AnimationTrackProperty.KeyFrame(Color.White, reveal + 0.08f),
                        new AnimationTrackProperty.KeyFrame(Color.White.WithAlpha(0f), 0.35f),
                    },
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(PointLightComponent),
                    Property = nameof(PointLightComponent.Energy),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(0f, 0),
                        new AnimationTrackProperty.KeyFrame(0f, reveal),
                        new AnimationTrackProperty.KeyFrame(5f, 0.02f),
                        new AnimationTrackProperty.KeyFrame(3f, 0.06f),
                        new AnimationTrackProperty.KeyFrame(0f, 0.35f),
                    },
                },
            },
        };
        _animation.Play(ent, animation, AnimationKey);
        _active.Add(ent.Owner);
    }
}
