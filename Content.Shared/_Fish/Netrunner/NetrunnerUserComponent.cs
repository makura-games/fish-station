using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Netrunner;

/// <summary>Private resource and remote-view state of the currently implanted player.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NetrunnerUserComponent : Component
{
    public override bool SendOnlyToOwner => true;
    [AutoNetworkedField] public EntityUid? Implant;
    [AutoNetworkedField] public EntityUid? Chair;
    [AutoNetworkedField] public EntityUid? Camera;
    /// <summary>Mobile camera-limited view, present only during a chair connection.</summary>
    [AutoNetworkedField] public EntityUid? RemoteEye;
    [AutoNetworkedField] public float Ram;
    [AutoNetworkedField] public float MaxRam;
    [AutoNetworkedField] public float Trace;
    [AutoNetworkedField] public TimeSpan ReadyAt;
    /// <summary>Whether a netrunner script is currently casting, used to predict cancel input cleanly.</summary>
    [AutoNetworkedField] public bool IsCasting;
}
