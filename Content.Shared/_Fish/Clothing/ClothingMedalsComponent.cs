using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Clothing;

/// <summary>
///     Хранит порядок, в котором к форме прикреплялись медали. Добавляется автоматически при первом прикреплении.
/// </summary>
/// <remarks>
///     Порядок ведёт сервер и передаёт клиентам, поэтому медаль на спрайте формы одинакова у всех наблюдателей,
///     в том числе у тех, кто подключился позже.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(ClothingMedalsSystem))]
public sealed partial class ClothingMedalsComponent : Component
{
    /// <summary>
    ///     Прикреплённые медали от самой ранней к последней.
    /// </summary>
    [AutoNetworkedField]
    public List<EntityUid> Order = new();
}
