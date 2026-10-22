// Необходимо поскольку этот partial класс расширяет ванильный тип и должен сохранять исходный namespace несмотря на расположение файла в _Fish
#pragma warning disable IDE0130 // Namespace does not match folder structure
using Content.Shared.Hands.Components;

namespace Content.Shared.Hands.EntitySystems;

public abstract partial class SharedHandsSystem
{
    /// <summary>
    /// Fish: включает/выключает стрип руки в Strip-UI в рантайме (например, при апгрейде
    /// борга на Mk2). Поле реплицируется через HandsComponentState, поэтому отдельного
    /// Dirty у вызывающего не требуется.
    /// </summary>
    public void SetCanBeStripped(Entity<HandsComponent?> ent, bool canBeStripped)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.CanBeStripped == canBeStripped)
            return;

        ent.Comp.CanBeStripped = canBeStripped;
        Dirty(ent);
    }
}
