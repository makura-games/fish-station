// Необходимо поскольку этот partial класс расширяет ванильный тип и должен сохранять исходный namespace несмотря на расположение файла в _Fish
#pragma warning disable IDE0130 // Namespace does not match folder structure
using Content.Shared.Hands.Components;

namespace Content.Shared.Hands.EntitySystems;

public abstract partial class SharedHandsSystem
{
    /// <summary>
    /// Fish: включает/выключает отображение предметов в руках на спрайте в рантайме
    /// (например, при апгрейде борга на Mk2). Поле реплицируется через HandsComponentState,
    /// поэтому отдельного Dirty у вызывающего не требуется.
    /// </summary>
    public void SetShowInHands(Entity<HandsComponent?> ent, bool show)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.ShowInHands == show)
            return;

        ent.Comp.ShowInHands = show;
        Dirty(ent);
    }
}
