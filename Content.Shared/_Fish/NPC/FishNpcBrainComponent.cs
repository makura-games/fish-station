using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.NPC;

/// <summary>
/// Fish-мозг враждебных NPC: режимы + доктрина штурм/оборона вместо тупого HTN chase.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishNpcBrainComponent : Component
{
    [DataField, AutoNetworkedField]
    public FishNpcDoctrine Doctrine = FishNpcDoctrine.Assault;

    [ViewVariables, AutoNetworkedField]
    public FishNpcMode Mode = FishNpcMode.Idle;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Target;

    /// <summary>
    /// Последняя известная позиция цели (поиск после потери LOS).
    /// </summary>
    [ViewVariables]
    public EntityCoordinates? LastSeenCoordinates;

    [ViewVariables]
    public TimeSpan ModeChangedAt;

    [ViewVariables]
    public TimeSpan NextModeEvaluate;

    /// <summary>
    /// Минимальное время удержания режима (анти-дёрганье).
    /// </summary>
    [DataField]
    public TimeSpan ModeHysteresis = TimeSpan.FromSeconds(0.6);

    [DataField]
    public TimeSpan ModeEvaluateInterval = TimeSpan.FromSeconds(0.25);

    [DataField]
    public float VisionRadius = 28f;

    [DataField]
    public float AggroVisionRadius = 35f;

    /// <summary>
    /// Предпочтительная дистанция стрельбы (оборона держит, штурм сжимает).
    /// </summary>
    [DataField]
    public float PreferRangedDistance = 8f;

    [DataField]
    public float MeleeCommitDistance = 2.2f;

    /// <summary>
    /// Штурм: переходить в мили раньше. Оборона: позже.
    /// </summary>
    [DataField]
    public float AssaultMeleeCommitDistance = 4.5f;

    [DataField]
    public float DefenseHoldMin = 5f;

    [DataField]
    public float DefenseHoldMax = 11f;

    [DataField]
    public float FleeHealthFraction = 0.2f;

    [DataField]
    public float SearchTimeoutSeconds = 8f;

    [ViewVariables]
    public float SearchAccumulator;

    /// <summary>
    /// Отключать vanilla HTN, пока мозг активен.
    /// </summary>
    [DataField]
    public bool DisableHtn = true;

    /// <summary>
    /// Идемпотентный флаг буста vision на экспедиции.
    /// </summary>
    [ViewVariables]
    public bool ExpeditionVisionBoosted;
}

[Serializable, NetSerializable]
public enum FishNpcDoctrine : byte
{
    /// <summary>Давит, сближается, CQC.</summary>
    Assault = 0,

    /// <summary>Держит дистанцию, отступает, перезаряжается под прикрытием.</summary>
    Defense = 1,
}

[Serializable, NetSerializable]
public enum FishNpcMode : byte
{
    Idle = 0,
    Hunt = 1,
    EngageRanged = 2,
    EngageMelee = 3,
    Reload = 4,
    Search = 5,
    Flee = 6,
}
