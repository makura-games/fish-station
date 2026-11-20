using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Ipc.Modules;

/// <summary>
/// Хост слотов модулей шасси КПБ (кап, контейнер, whitelist).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedFishIpcModuleSystem))]
public sealed partial class FishIpcModuleHostComponent : Component
{
    /// <summary>Максимум установленных модулей (включая пассивные).</summary>
    [DataField, AutoNetworkedField]
    public int MaxModules = 3;

    [DataField]
    public string ModuleContainerId = "ipc_module";

    [ViewVariables]
    public Container ModuleContainer = default!;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? ModuleWhitelist;

    [ViewVariables]
    public int ModuleCount => ModuleContainer.ContainedEntities.Count;

    /// <summary>Секундный накопитель пассивного расхода модулей.</summary>
    [ViewVariables]
    public float PassiveDrainTimer;

    [ViewVariables]
    public float PassiveDrainPending;
}
