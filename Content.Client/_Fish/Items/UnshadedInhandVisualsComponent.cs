namespace Content.Client._Fish.Items;

/// <summary>
/// Добавляет поверх стандартного спрайта предмета в руках дополнительный слой с <c>shader: unshaded</c>,
/// используя имя текущего стейта (включая <c>HeldPrefix</c>, например <c>wielded-</c>) с указанным суффиксом.
/// </summary>
[RegisterComponent]
public sealed partial class UnshadedInhandVisualsComponent : Component
{
    /// <summary>
    /// Суффикс стейта в RSI для светящегося слоя (например, <c>-light</c>).
    /// </summary>
    [DataField]
    public string Suffix = "-light";
}
