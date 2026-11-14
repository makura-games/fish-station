namespace Content.Shared._Fish.ObrCall;

/// <summary>
/// Маркер активного крио-развёртывания ОБР с консоли ЦК.
/// </summary>
[RegisterComponent]
public sealed partial class ObrCryoDeploymentComponent : Component
{
    /// <summary>
    /// Id прототипа отряда.
    /// </summary>
    [ViewVariables]
    public string TeamId = string.Empty;

    /// <summary>
    /// Текст миссии для брифинга.
    /// </summary>
    [ViewVariables]
    public string Mission = string.Empty;

    /// <summary>
    /// Заспавненные члены отряда (ghost-role гуманоиды).
    /// </summary>
    [ViewVariables]
    public List<EntityUid> SpawnedMembers = new();
}
