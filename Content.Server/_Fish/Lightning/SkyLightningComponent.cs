namespace Content.Server._Fish.Lightning;

/// <summary>
/// Однократный удар молнии по области при появлении сущности.
/// </summary>
[RegisterComponent]
public sealed partial class SkyLightningComponent : Component
{
    /// <summary>
    /// Радиус поражения в тайлах.
    /// </summary>
    [DataField]
    public float Radius = 3.5f;

    /// <summary>
    /// Электрический урон до применения изоляции.
    /// </summary>
    [DataField]
    public int ShockDamage = 40;

    /// <summary>
    /// Продолжительность оглушения электрическим разрядом.
    /// </summary>
    [DataField]
    public TimeSpan StunDuration = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Максимальное отклонение молнии от вертикали в градусах.
    /// </summary>
    [DataField]
    public float MaxTilt = 8f;

    /// <summary>
    /// Ключ триггера, срабатывающего при достижении разрядом земли.
    /// </summary>
    [DataField]
    public string StrikeKey = "lightning-impact";

    /// <summary>
    /// Удар уже произошёл; повторное применение запрещено.
    /// </summary>
    public bool Struck;
}
