using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Ipc;

/// <summary>Звуковые сбои при повреждениях (Paradise: beep/error messages).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishEmitBuzzWhileDamagedComponent : Component
{
    [DataField]
    public TimeSpan BuzzPopupCooldown = TimeSpan.FromSeconds(8);

    [ViewVariables]
    public TimeSpan LastBuzzPopupTime;

    /// <summary>Интервал жужжания в крите/при сильных повреждениях (сек).</summary>
    [DataField]
    public float CycleDelay = 6f;

    public float AccumulatedFrametime;

    [DataField]
    public SoundSpecifier Sound = new SoundCollectionSpecifier("FishIpcBuzzes"); // Fish-edit: buzzes КПБ
}
