using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Mobs.Mimic;

/// <summary>
/// Прыжок мимика.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MimicJumpComponent : Component
{
    /// <summary>
    /// Прототип действия, отвечающего за прыжок.
    /// </summary>
    [DataField]
    public EntProtoId ActionId = "ActionMimicJump";

    /// <summary>
    /// Максимальная дальность прыжка.
    /// </summary>
    [DataField]
    public float MaxJumpDistance = 10f;

    /// <summary>
    /// Скорость, с которой мимик прыгает.
    /// </summary>
    [DataField]
    public float JumpSpeed = 7f;

    /// <summary>
    /// Сила отдачи при прыжке.
    /// </summary>
    [DataField]
    public float PushbackRatio = 10f;

    /// <summary>
    /// Сколько секунд целю задетая прыжком лежит без сознания.
    /// </summary>
    [DataField]
    public float ParalyzeTime = 5f;

    /// <summary>
    /// Урон, который наносит прыжок задетой цели.
    /// </summary>
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Slash", 10 },
        },
    };
}
