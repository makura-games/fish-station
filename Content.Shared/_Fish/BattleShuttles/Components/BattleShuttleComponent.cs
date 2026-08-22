using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.BattleShuttles.Components;

/// <summary>
/// Маркер боевого шаттла — тонкая специализация <c>Mech</c>.
/// Пилот, батарея, BUI, воздух, EMP и оружие обслуживает Mech; здесь только люк, замок и класс.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BattleShuttleComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Unlocked = true;

    /// <summary>
    /// Сервисный люк. Не путать с MechVisuals.Open (кабина пуста/занята).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool HatchOpen = true;

    [DataField]
    public int BasePassengerCapacity;

    [DataField, AutoNetworkedField]
    public int MaxPassengers;

    [DataField, AutoNetworkedField]
    public bool HasLock;

    [DataField, AutoNetworkedField]
    public int LockId;

    /// <summary>
    /// Класс шаттла для CompatibleShuttleTags модулей.
    /// </summary>
    [DataField]
    public List<ProtoId<TagPrototype>> ClassTags = [];

    [DataField]
    public bool RequireOpenHatchForInstall = true;

    [DataField]
    public float BaseFixtureDensity = 100f;

    [DataField]
    public EntProtoId ToggleLockAction = "ActionBattleShuttleToggleLock";

    [DataField]
    public EntityUid? ToggleLockActionEntity;

    /// <summary>
    /// Кэш: на шаттле есть модуль с OreScoop. Обновляется при insert/remove.
    /// </summary>
    [ViewVariables]
    public bool HasActiveOreScoop;
}
