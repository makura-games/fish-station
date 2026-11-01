// Необходимо поскольку этот partial класс расширяет ванильный тип и должен сохранять исходный namespace несмотря на расположение файла в _Fish
#pragma warning disable IDE0130 // Namespace does not match folder structure
using System.Numerics;
using static Robust.Client.GameObjects.SpriteComponent;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.Hands.Systems;

public sealed partial class HandsSystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    /// <summary>
    /// Fish: компенсирует вертикальное смещение in-hand слоя для боргов Mk2.
    /// Вызывается после LayerSetData: у боргов со смещённым корнем спрайта
    /// (SpriteOffset у borgType) предметы в руках поднимаются вместе с телом
    /// и оказываются чуть выше нужного. Результат <see cref="GetMk2HandOffsetCompensation"/>
    /// вычисляется вызывающим один раз на UpdateHandVisuals вне цикла по слоям
    /// и передаётся сюда, чтобы не повторять TryComp/TryIndex для каждого слоя.
    /// </summary>
    private void ApplyMk2HandOffsetCompensation(
        Entity<SpriteComponent> spriteEnt,
        int layerIndex,
        Vector2? compensation)
    {
        if (compensation is not { } offset)
            return;

        if (spriteEnt.Comp[layerIndex] is not Layer layer)
            return;

        _sprite.LayerSetOffset(layer, layer.Offset + offset);
    }

    /// <summary>
    /// Fish: вычисляет вертикальную компенсацию in-hand слоёв из геометрии выбранного borgType.
    /// Берётся половина <see cref="BorgTypePrototype.SpriteOffset"/> по Y в мировых единицах
    /// и переводится в единицы слоя через <see cref="BorgTypePrototype.SpriteScale"/>,
    /// поскольку LayerSetOffset масштабируется корневым Scale спрайта.
    /// Возвращает <c>null</c>, если у сущности нет BorgSwitchableTypeComponent,
    /// выбранный тип отсутствует или не задаёт вертикальный SpriteOffset.
    /// </summary>
    private Vector2? GetMk2HandOffsetCompensation(EntityUid uid)
    {
        if (!TryComp<BorgSwitchableTypeComponent>(uid, out var switchable))
            return null;

        if (switchable.SelectedBorgType is not { } borgType)
            return null;

        if (!_prototype.TryIndex(borgType, out BorgTypePrototype? prototype)
            || prototype.SpriteOffset is not { } offset
            || offset.Y == 0f)
        {
            return null;
        }

        var scale = prototype.SpriteScale is { } spriteScale && spriteScale != 0f
            ? spriteScale
            : 1f;

        return new Vector2(0f, -offset.Y / (2f * scale));
    }
}
