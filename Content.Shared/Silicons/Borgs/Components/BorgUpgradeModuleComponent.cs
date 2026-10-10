namespace Content.Shared.Silicons.Borgs.Components;

/// <summary>
/// Предмет-апгрейд, превращающий обычный cyborg в его Mk2-вариант.
/// Механика реализована в <c>Content.Server.Silicons.Borgs.BorgSwitchableTypeSystem.Upgrade</c>.
/// </summary>
[RegisterComponent]
public sealed partial class BorgUpgradeModuleComponent : Component
{
    /// <summary>
    /// Длительность непрерывного апгрейда.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(15);
}
