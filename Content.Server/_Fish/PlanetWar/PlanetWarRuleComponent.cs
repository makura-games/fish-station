using Content.Shared._Fish.PlanetWar;

namespace Content.Server._Fish.PlanetWar;

/// <summary>
/// Данные активного правила PlanetWar (победа, таймер рестарта).
/// </summary>
[RegisterComponent, Access(typeof(PlanetWarRuleSystem))]
public sealed partial class PlanetWarRuleComponent : Component
{
    /// <summary>
    /// Задержка перед рестартом раунда после объявления победителя.
    /// </summary>
    [DataField]
    public TimeSpan RoundEndDelay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Победившая команда; null — раунд ещё идёт или ничья.
    /// </summary>
    [ViewVariables]
    public PlanetWarTeam? Winner;

    /// <summary>
    /// Раунд уже завершается — повторные уничтожения врат игнорируются.
    /// </summary>
    [ViewVariables]
    public bool Ending;
}
