// Необходимо поскольку этот partial класс расширяет ванильный тип и должен сохранять исходный namespace несмотря на расположение файла в _Fish
#pragma warning disable IDE0130 // Namespace does not match folder structure
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Hands.Components;
using Robust.Client.GameObjects;

namespace Content.Client.Hands.Systems;

public sealed partial class HandsSystem
{
    /// <summary>
    /// Fish: применяет сетевое состояние <see cref="HandsComponentState.ShowInHands"/> к компоненту
    /// и сообщает, изменился ли флаг. Сама смена флага не вызывает UpdateHandVisuals —
    /// вызывающий после обработки состояния решает, перерисовывать ли занятые руки.
    /// </summary>
    private bool ApplyShowInHandsState(Entity<HandsComponent> ent, HandsComponentState state)
    {
        var showInHandsChanged = state.ShowInHands != ent.Comp.ShowInHands;
        ent.Comp.ShowInHands = state.ShowInHands;
        return showInHandsChanged;
    }

    /// <summary>
    /// Fish: готовит набор RevealedLayers для руки перед обновлением in-hand визуала:
    /// очищает (и удаляет со спрайта) старые слои, возвращаем false — когда обновлять нечего.
    /// </summary>
    /// <remarks>
    /// Порядок важен и совпадает с исходной правкой: очистка старых слоёв идёт
    /// до раннего выхода по ShowInHands — при выключении флага (true -> false)
    /// иначе ранее добавленные слои остаются на спрайте (HandleComponentState
    /// вызывает UpdateHandVisuals при смене флага в обе стороны).
    /// Привязка — по handId (а не HandLocation), чтобы несколько Functional-рук
    /// не разделяли один HashSet.
    /// </remarks>
    /// <param name="uid">Сущность с HandsComponent и спрайтом.</param>
    /// <param name="handComp">Компонент рук.</param>
    /// <param name="sprite">Спрайт сущности (для удаления старых слоёв).</param>
    /// <param name="handId">ID руки — ключ привязки RevealedLayers.</param>
    /// <param name="revealedLayers">Набор слоёв руки; не null при true.</param>
    /// <returns>false — визуал обновлять не нужно (ShowInHands выключен).</returns>
    private bool TryPrepareRevealedLayers(
        EntityUid uid,
        HandsComponent handComp,
        SpriteComponent sprite,
        string handId,
        [NotNullWhen(true)] out HashSet<string>? revealedLayers)
    {
        if (handComp.RevealedLayers.TryGetValue(handId, out revealedLayers))
        {
            foreach (var key in revealedLayers)
            {
                _sprite.RemoveLayer((uid, sprite), key);
            }

            revealedLayers.Clear();
        }

        if (!handComp.ShowInHands)
            return false;

        if (revealedLayers == null)
        {
            revealedLayers = new();
            handComp.RevealedLayers[handId] = revealedLayers;
        }

        return true;
    }
}
