using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.BattleShuttles.Components;

/// <summary>
/// Автосбор руды/предметов по тегу при движении шаттла. Параметры задаются в Prototype.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BattleShuttleOreScoopComponent : Component
{
    [DataField]
    public float Range = 1.5f;

    [DataField]
    public ProtoId<TagPrototype> ScoopTag = "Ore";
}
