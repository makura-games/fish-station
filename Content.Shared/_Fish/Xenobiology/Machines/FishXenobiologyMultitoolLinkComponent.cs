namespace Content.Shared._Fish.Xenobiology.Machines;

/// <summary>
/// Буфер игрока: мультитулом выбрана консоль для линка ресайклера.
/// </summary>
[RegisterComponent]
public sealed partial class FishXenobiologyMultitoolLinkComponent : Component
{
    public EntityUid? PendingConsole;
}
