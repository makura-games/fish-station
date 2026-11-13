using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.ObrCall;

/// <summary>
/// Спавнер роли ОБР и ключ точки крио на ЦК (Leader / Engineer / CBURN / …).
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class ObrCryoRoleSpawner
{
    [DataField(required: true)]
    public EntProtoId Spawner = string.Empty;

    /// <summary>
    /// Ключ для сопоставления с SpawnOnTrigger на карте ЦК.
    /// Одинаковый ключ у нескольких спавнеров → несколько слотов одной роли (Security ×3).
    /// </summary>
    [DataField(required: true)]
    public string RoleKey = string.Empty;
}
