namespace Content.Client._Fish.Psychosis;

/// <summary>
/// Маркер для клиентских галлюцинаций.
/// </summary>
[RegisterComponent]
public sealed partial class HallucinationComponent : Component
{
    /// <summary>
    /// Время создания (для плавного исчезновения).
    /// </summary>
    public TimeSpan CreatedAt;

    /// <summary>
    /// Общее время жизни.
    /// </summary>
    public TimeSpan Lifetime;

    /// <summary>
    /// Начальная альфа спрайта.
    /// </summary>
    public float StartAlpha = 1f;
}