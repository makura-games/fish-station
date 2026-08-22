using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Mechs.Components;

/// <summary>
/// Сервисный режим Fish Mech: Ready → ServiceHold → AccessPanel.
/// Любое состояние кроме Ready блокирует движение и установку модулей.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechMaintenanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public MechMaintenanceState State = MechMaintenanceState.Ready;

    /// <summary>
    /// Можно ли пилоту/инструментом включать ServiceHold (combat часто false).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool MaintAccess = true;
}

/// <summary>
/// Три стадии сервиса (не пятиступенчатая болтовая лестница).
/// </summary>
[Serializable, NetSerializable]
public enum MechMaintenanceState : byte
{
    /// <summary>Штатная эксплуатация.</summary>
    Ready = 0,
    /// <summary>Сервисный холд: движение запрещено.</summary>
    ServiceHold = 1,
    /// <summary>Открыта сервисная панель (доступ к ремонту).</summary>
    AccessPanel = 2,
}
