namespace Content.Shared._Fish.BorgRest;

/// <summary>
/// Fish: маркер действия, разрешённого боргу во время отдыха. Ставится только на само
/// действие отдыха — оно должно работать в состоянии покоя, иначе борг не сможет встать.
/// Блокировка остальных действий — в SharedBorgRestSystem.
/// </summary>
[RegisterComponent]
public sealed partial class BorgRestAllowedMarkerComponent : Component
{
}
