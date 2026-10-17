// Необходимо поскольку этот partial класс расширяет ванильный тип и должен сохранять исходный namespace несмотря на расположение файла в _Fish
#pragma warning disable IDE0130 // Namespace does not match folder structure
using System.Numerics;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.GameObjects;

namespace Content.Client.Silicons.Borgs;

public sealed partial class BorgSwitchableTypeSystem
{
    /// <summary>
    /// Fish: применяет к спрайту визуал выбранного типа — RSI, состояния слоёв,
    /// корневое смещение (SpriteOffset, например 0,0.4 у Mk2) и масштаб (SpriteScale, 0.8).
    /// </summary>
    /// <remarks>
    /// Вызывается из UpdateEntityAppearance строго до LayerSetRsiState.
    /// </remarks>
    private void ApplyBorgTypeSpriteTransform(
        Entity<BorgSwitchableTypeComponent> entity,
        SpriteComponent sprite,
        BorgTypePrototype prototype)
    {
        // Mk2 рисуется из отдельного холста (_Lust), а RSI слоёв у живой сущности
        // остался от родительского chassis. Меняем его до выставления state, иначе
        // state вроде sec_mk2* будет искаться в vanilla-RSI и даст ERRO.
        if (prototype.SpriteRsiPath is { } rsiPath)
        {
            _sprite.LayerSetRsi((entity, sprite), BorgVisualLayers.Body, rsiPath);
            _sprite.LayerSetRsi((entity, sprite), BorgVisualLayers.Light, rsiPath);
            _sprite.LayerSetRsi((entity, sprite), BorgVisualLayers.LightStatus, rsiPath);
        }

        // Сдвиг уходит на корень спрайта: так runtime-слои предметов (in-hand,
        // надетая экипировка) наследуют сдвиг и едут вместе с телом, а не отстают.
        // Присваиваем безусловно, чтобы сбросить сдвиг у типов без него.
        _sprite.SetOffset((entity.Owner, sprite), prototype.SpriteOffset ?? Vector2.Zero);

        // Fish: масштаб корня спрайта (Mk2 — 0.8): применяется ко всему дереву
        // слоёв, включая in-hand предметы и одежду. Безусловный сброс в 1 —
        // как у Offset, чтобы возврат к vanilla-типу вернул исходный размер.
        var scale = prototype.SpriteScale is { } spriteScale ? new Vector2(spriteScale, spriteScale) : Vector2.One;
        _sprite.SetScale((entity.Owner, sprite), scale);
    }
}
