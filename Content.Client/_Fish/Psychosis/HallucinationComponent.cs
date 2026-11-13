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
    /// Время начала затухания, если оно уже запущено.
    /// </summary>
    public TimeSpan? FadeStartedAt;

    /// <summary>
    /// Начальная альфа спрайта.
    /// </summary>
    public float StartAlpha = 1f;

    /// <summary>
    /// Мираж, который рассеивается, если подойти вплотную.
    /// </summary>
    public bool IsMirage;
}