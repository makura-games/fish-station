using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Ipc;

/// <summary>
/// Стартовый набор модулей шасси КПБ для боевой/специальной роли (ОБР, NukeOps и т.п.).
/// </summary>
[Prototype]
public sealed partial class FishIpcRoleModuleLoadoutPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Job / antagSpecifier id, для которого выдаётся набор (например ERTSecurity, NukeopsMedic).
    /// </summary>
    [DataField(required: true)]
    public string Role = string.Empty;

    /// <summary>
    /// Модули по порядку установки. Обычно EmpScreen первым, далее по профи.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> Modules = new();

    /// <summary>
    /// Батарея шасси (для ОБР/NukeOps — PowerCellSyndicate из аплинка).
    /// </summary>
    [DataField]
    public EntProtoId? PowerCell;

    /// <summary>
    /// Запретить изъятие/замену батареи после установки <see cref="PowerCell"/>.
    /// </summary>
    [DataField]
    public bool SealPowerCell;
}
