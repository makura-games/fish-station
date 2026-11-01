namespace Content.Shared._Fish.Hands;

/// <summary>
///     Висит на персонаже, который протягивает предмет другому. Получатель забирает предмет через Alt + ЛКМ по протягивающему.
/// </summary>
/// <remarks>
///     Предложение живёт только на сервере и не сохраняется: оно сбрасывается при новом предложении,
///     после передачи или когда предмет перестаёт быть в руках у протягивающего.
/// </remarks>
[RegisterComponent]
public sealed partial class HandOfferComponent : Component
{
    /// <summary>
    ///     Протягиваемый предмет.
    /// </summary>
    public EntityUid Item;

    /// <summary>
    ///     Тот, кому протягивают предмет. Забрать предмет может только он.
    /// </summary>
    public EntityUid Target;
}
