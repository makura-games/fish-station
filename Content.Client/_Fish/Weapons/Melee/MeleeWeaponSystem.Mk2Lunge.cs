// Необходимо поскольку этот partial класс расширяет ванильный тип и должен сохранять исходный namespace несмотря на расположение файла в _Fish
#pragma warning disable IDE0130 // Namespace does not match folder structure
using System.Numerics;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Prototypes;

namespace Content.Client.Weapons.Melee;

public sealed partial class MeleeWeaponSystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    /// <summary>
    /// Fish: постоянное смещение корня спрайта для базы lunge-анимации.
    /// Для боргов берётся <see cref="BorgTypePrototype.SpriteOffset"/> выбранного типа
    /// (он применяется на лету к SpriteComponent и отсутствует в прототипе шасси),
    /// иначе — offset из компонента Sprite прототипа сущности, иначе Vector2.Zero.
    /// Текущий SpriteComponent.Offset не используется: во время lunge он содержит
    /// промежуточный кадр анимации.
    /// </summary>
    private Vector2 GetBaseSpriteOffset(EntityUid uid)
    {
        if (TryComp(uid, out BorgSwitchableTypeComponent? switchable)
            && switchable.SelectedBorgType is { } borgType
            && _prototype.TryIndex(borgType, out BorgTypePrototype? prototype))
        {
            return prototype.SpriteOffset ?? Vector2.Zero;
        }

        if (MetaData(uid).EntityPrototype?.Components.TryGetComponent("Sprite", out var spriteComp) == true
            && spriteComp is SpriteComponent protoSprite)
        {
            return protoSprite.Offset;
        }

        return Vector2.Zero;
    }

    /// <summary>
    /// Fish: lunge-анимация для удара: база — постоянное смещение корня спрайта
    /// (borgType или прототип), а не текущий Offset — во время уже идущего lunge
    /// Offset содержит промежуточный кадр, и повторный удар закрепил бы его как
    /// базовый — спрайт бы «дрейфовал». Для мобов без смещения поведение не меняется.
    /// lungeScale — амплитуда в мировых метрах (корневой Offset Scale не масштабируется),
    /// поэтому для уменьшенных спрайтов (Mk2, Scale 0.75) вылет приводим к масштабу
    /// тела — иначе смещение выглядело бы относительно сильнее.
    /// </summary>
    private Animation GetLungeAnimation(EntityUid user, Vector2 localPos)
    {
        var baseOffset = GetBaseSpriteOffset(user);
        var lungeScale = TryComp(user, out SpriteComponent? userSprite) ? userSprite.Scale.X : 1f;
        return GetLungeAnimation(localPos, baseOffset, lungeScale);
    }

    /// <summary>
    /// Fish: lunge-анимация с учётом базового смещения спрайта и его масштаба.
    /// Keyframes считаются от baseOffset (базовый Offset спрайта), чтобы не затирать
    /// пользовательские смещения — иначе после lunge Offset сбрасывается в ноль и не восстанавливается.
    /// lungeScale — масштаб спрайта атакующего: амплитуда задана в мировых метрах
    /// (корневой Offset Scale не масштабируется), для уменьшенных спрайтов (Mk2) вылет
    /// уменьшается пропорционально телу.
    /// </summary>
    private Animation GetLungeAnimation(Vector2 direction, Vector2 baseOffset, float lungeScale = 1f)
    {
        const float length = 0.1f;

        var amplitude = 0.15f * lungeScale;

        return new Animation
        {
            Length = TimeSpan.FromSeconds(length),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(baseOffset, 0f),
                        new AnimationTrackProperty.KeyFrame(baseOffset + direction.Normalized() * amplitude, length*0.4f),
                        new AnimationTrackProperty.KeyFrame(baseOffset, length*0.6f),
                    },
                },
            },
        };
    }
}
