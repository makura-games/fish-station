namespace Content.Shared._Fish.Xenobiology.Slimes;

/// <summary>
/// Слайм в «загоне» консоли: без охоты и роста по AI-тику.
/// </summary>
[RegisterComponent]
public sealed partial class FishSlimePenStoredComponent : Component
{
    public EntityUid? Console;
}
