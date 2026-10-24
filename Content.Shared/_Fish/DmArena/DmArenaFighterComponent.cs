using Robust.Shared.GameObjects;

namespace Content.Shared._Fish.DmArena;

[RegisterComponent]
public sealed partial class DmArenaFighterComponent : Component
{
    /// <summary>
    /// Сущность правила арены, к которому привязан игрок.
    /// </summary>
    [ViewVariables]
    public EntityUid? RuleEntity;

    /// <summary>
    /// Последний участник арены, атаковавший игрока.
    /// </summary>
    [ViewVariables]
    public EntityUid? LastAttacker;
}
