using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.PlanetWar;

/// <summary>
/// Всплывающий брифинг при взятии гост-роли / посадке разума (цвет, звук, текст).
/// По подобию XenoborgComponent, без привязки к antag mind-role.
/// </summary>
[RegisterComponent]
public sealed partial class PlanetWarBriefingComponent : Component
{
    /// <summary>
    /// Текст брифинга в чат при взятии роли.
    /// </summary>
    [DataField(required: true)]
    public LocId BriefingText;

    /// <summary>
    /// Цвет текста брифинга (обычно цвет департамента фракции).
    /// </summary>
    [DataField]
    public Color BriefingColor = Color.White;

    /// <summary>
    /// Звук при появлении брифинга. null — без звука.
    /// </summary>
    [DataField]
    public SoundSpecifier? BriefingSound;
}
