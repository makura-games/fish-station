using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Ipc.Modules;

/// <summary>
/// Медленный пассивный ремонт шасси КПБ. Работает только при достаточном заряде.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcNanoRepairModuleComponent : Component
{
    /// <summary>Отрицательный урон в секунду (лечение).</summary>
    [DataField(required: true)]
    public DamageSpecifier HealPerSecond = new();
}
