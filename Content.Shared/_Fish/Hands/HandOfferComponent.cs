using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Hands;

/// <summary>
///     Висит на персонаже, который протягивает предмет другому. Получатель забирает предмет через Alt + ЛКМ по протягивающему.
/// </summary>
/// <remarks>
///     Предложение сбрасывается новым предложением, после передачи или когда предмет покидает руки протягивающего.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(HandOfferSystem))]
public sealed partial class HandOfferComponent : Component
{
    /// <summary>
    ///     Протягиваемый предмет.
    /// </summary>
    [AutoNetworkedField]
    public EntityUid Item;

    /// <summary>
    ///     Тот, кому протягивают предмет. Забрать предмет может только он.
    /// </summary>
    [AutoNetworkedField]
    public EntityUid Target;
}
