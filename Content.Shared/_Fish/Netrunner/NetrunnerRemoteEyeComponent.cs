using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Netrunner;

/// <summary>Nonphysical movement proxy. It grants neither AI privileges nor ghost visibility.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NetrunnerRemoteEyeComponent : Component
{
    /// <summary>Body that controls this viewer and remains vulnerable in the chair.</summary>
    [AutoNetworkedField] public EntityUid User;
    /// <summary>Only cameras belonging to this station may provide vision.</summary>
    [AutoNetworkedField] public EntityUid Station;
    /// <summary>Active script cast window for rendering progress above the netrunner eye.</summary>
    [AutoNetworkedField] public TimeSpan CastStart;
    [AutoNetworkedField] public TimeSpan CastEnd;
}
