using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Silicons.Borgs;

/// <summary>
/// Завершение апгрейда борга до Mk2 предметом BorgUpgradeModule.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BorgUpgradeDoAfterEvent : SimpleDoAfterEvent
{
}
