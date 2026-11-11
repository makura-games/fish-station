namespace Content.Client._Fish.Lightning;

/// <summary>
/// Привязывает нижний конец молнии к точке удара при наклоне спрайта.
/// </summary>
[RegisterComponent]
public sealed partial class SkyLightningVisualsComponent : Component
{
    /// <summary>
    /// Расстояние от нижнего края до центра спрайта в тайлах.
    /// </summary>
    [DataField]
    public float HalfHeight = 6f;

    /// <summary>
    /// Длительность раскрытия разряда; должна совпадать с задержкой TimerTrigger.
    /// </summary>
    [DataField]
    public float RevealDuration = 0.12f;

    /// <summary>
    /// Анимация уже запускалась; обновления Appearance не должны повторять удар.
    /// </summary>
    public bool Played;
}
