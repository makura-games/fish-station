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
    /// Fish: компенсирует смещение in-hand слоя (-0.25 по Y) для боргов Mk2.
    /// Вызывается после LayerSetData: у боргов со смещённым корнем спрайта
    /// (SpriteOffset у borgType) предметы в руках поднимаются вместе с телом
    /// и оказываются чуть выше нужного. Результат <see cref="NeedsMk2HandOffsetCompensation"/>
    /// вычисляется вызывающим один раз на UpdateHandVisuals вне цикла по слоям
    /// и передаётся сюда, чтобы не повторять TryComp/TryIndex для каждого слоя.
    /// </summary>
    private void ApplyMk2HandOffsetCompensation(Entity<SpriteComponent> spriteEnt, int layerIndex, bool needsCompensation)
    {
        if (!needsCompensation)
            return;

        if (spriteEnt.Comp[layerIndex] is not Layer layer)
            return;

        _sprite.LayerSetOffset(layer, layer.Offset + new Vector2(0f, -0.25f));
    }

    /// <summary>
    /// Fish: нужна ли компенсация смещения in-hand слоёв (-0.25 по Y): только для боргов,
    /// чей выбранный borgType задаёт ненулевой <see cref="BorgTypePrototype.SpriteOffset"/>
    /// (Mk2 со смещённым на 0,0.4 корнем спрайта). Сущности без BorgSwitchableTypeComponent
    /// или с типом без SpriteOffset компенсация не применяется.
    /// Компенсация задаётся в слойных единицах и масштабируется корневым Scale спрайта
    /// вместе с руками тела — при SpriteScale 0.8 визуально становится -0.2.
    /// </summary>
    private bool NeedsMk2HandOffsetCompensation(EntityUid uid)
    {
        if (!TryComp<BorgSwitchableTypeComponent>(uid, out var switchable))
            return false;

        if (switchable.SelectedBorgType is not { } borgType)
            return false;

        return _prototype.TryIndex(borgType, out BorgTypePrototype? prototype)
            && prototype.SpriteOffset is { } offset
            && offset != Vector2.Zero;
    }
}
