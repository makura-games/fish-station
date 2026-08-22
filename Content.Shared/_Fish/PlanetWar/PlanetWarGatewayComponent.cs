using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.PlanetWar;

/// <summary>
/// Маркер врат фракции PlanetWar. Уничтожение обрабатывает <c>PlanetWarRuleSystem</c>.
/// </summary>
[RegisterComponent]
public sealed partial class PlanetWarGatewayComponent : Component
{
    /// <summary>
    /// Команда, которой принадлежат эти врата.
    /// </summary>
    [DataField(required: true)]
    public PlanetWarTeam Team;
}

[Serializable, NetSerializable]
public enum PlanetWarTeam : byte
{
    Core = 0,
    Arm = 1,
}
