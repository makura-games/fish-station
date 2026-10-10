using Content.Client.UserInterface.Systems.Hands.Controls;
using Content.Shared.Hands.Components;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.UserInterface.Systems.Hotbar.Widgets;

public sealed partial class HotbarGui
{
    // Fish: пока у сущности нет обычных (не-Functional) рук — например, у борга без chassis-рук —
    // модульные Functional-руки показываем в главном ряду рук (старое отображение module HUD),
    // а не в отдельном Functional-ряду. Значение пересчитывается при добавлении/удалении рук.
    public bool FunctionalInMainRow;

    public void ClearHandButtons()
    {
        HandContainer.ClearButtons();
        FunctionalHandContainer.ClearButtons();
    }

    public void SetPlayerHandsComponent(Entity<HandsComponent> hands)
    {
        HandContainer.PlayerHandsComponent = hands.Comp;
        FunctionalHandContainer.PlayerHandsComponent = hands.Comp;
        UpdateFunctionalRouting(hands.Comp);
    }

    public bool TryGetHandButton(string handName, out HandButton? handButton)
    {
        if (HandContainer.TryGetButton(handName, out handButton))
            return true;

        return FunctionalHandContainer.TryGetButton(handName, out handButton);
    }

    public HandButton? GetHandButton(string handName)
    {
        return TryGetHandButton(handName, out var handButton) ? handButton : null;
    }

    public void AddHandButton(HandButton handButton)
    {
        GetHandContainer(handButton.HandLocation).TryAddButton(handButton);
    }

    public bool TryRemoveHandButton(string handName, out HandButton? handButton)
    {
        if (HandContainer.TryRemoveButton(handName, out handButton))
            return true;

        return FunctionalHandContainer.TryRemoveButton(handName, out handButton);
    }

    public IEnumerable<HandButton> GetHandButtons()
    {
        foreach (var handButton in HandContainer.GetButtons())
        {
            yield return handButton;
        }

        foreach (var handButton in FunctionalHandContainer.GetButtons())
        {
            yield return handButton;
        }
    }

    private HandsContainer GetHandContainer(HandLocation location)
    {
        if (location == HandLocation.Functional && !FunctionalInMainRow)
            return FunctionalHandContainer;

        return HandContainer;
    }

    /// <summary>
    /// Fish: пересчитывает, в каком контейнере показывать Functional-руки, и при смене
    /// решения переносит уже добавленные Functional-кнопки в нужный контейнер.
    /// excludeHandName нужен из-за порядка событий: OnPlayerRemoveHand поднимается до
    /// удаления руки из компонента, поэтому исходящая рука исключается из проверки.
    /// </summary>
    public void UpdateFunctionalRouting(HandsComponent hands, string? excludeHandName = null)
    {
        // Пока не найдена ни одна не-Functional рука — считаем, что их нет.
        var mainRow = true;

        foreach (var (id, hand) in hands.Hands)
        {
            if (id == excludeHandName)
                continue;

            if (hand.Location != HandLocation.Functional)
            {
                mainRow = false;
                break;
            }
        }

        if (mainRow == FunctionalInMainRow)
            return;

        FunctionalInMainRow = mainRow;
        RelocateFunctionalButtons();
    }

    private void RelocateFunctionalButtons()
    {
        var target = GetHandContainer(HandLocation.Functional);

        // Снимок: перенос мутирует контейнеры, по которым идёт перебор.
        var functionalButtons = new List<HandButton>();

        foreach (var button in FunctionalHandContainer.GetButtons())
        {
            if (button.HandLocation == HandLocation.Functional)
                functionalButtons.Add(button);
        }

        foreach (var button in HandContainer.GetButtons())
        {
            if (button.HandLocation == HandLocation.Functional)
                functionalButtons.Add(button);
        }

        foreach (var button in functionalButtons)
        {
            // Уже в целевом контейнере — не трогаем, чтобы не ломать порядок.
            if (target.TryGetButton(button.SlotName, out _))
                continue;

            if (!HandContainer.TryRemoveButton(button.SlotName, out _))
                FunctionalHandContainer.TryRemoveButton(button.SlotName, out _);

            target.TryAddButton(button);
        }
    }
}
