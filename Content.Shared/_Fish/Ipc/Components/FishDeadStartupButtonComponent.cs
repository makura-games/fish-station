using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Ipc;

/// <summary>Кнопка перезапуска «мёртвого» шасси (EE DeadStartupButton).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishDeadStartupButtonComponent : Component
{
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_Fish/Effects/Silicon/startup.ogg");

    [DataField]
    public TimeSpan DoAfterDelay = TimeSpan.FromSeconds(5);
}
