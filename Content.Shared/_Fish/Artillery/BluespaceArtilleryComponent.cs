using Content.Shared.Sound;
using Content.Shared.Maps;
using Content.Shared.GameTicking;
using Content.Shared.DeviceLinking;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;

namespace Content.Shared._Fish.Artillery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedBluespaceArtillerySystem))]
public sealed partial class BluespaceArtilleryComponent : Component
{
    [DataField]
	public float ChargeDuration = 8.5f;

	[DataField]
	public float FlightDuration = 12.0f;

	[DataField]
	public float CooldownDuration = 60.0f;

	[DataField]
	public SoundSpecifier SectorChargeSound = default!;

	[DataField]
	public SoundSpecifier ChargeSound = default!;

	[DataField]
	public SoundSpecifier FireSound = default!;

	[DataField]
	public SoundSpecifier ImpactSound = default!;

	[DataField]
	public string ChargingState = "charging";

	[DataField]
	public string FireState = "fire";

	[DataField]
	public string LinkingPort = "Artillery";
	
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Console;

    [ViewVariables]
    public EntityUid? LinkedConsole;

    [ViewVariables]
    public bool IsCharging;
	
	[ViewVariables]
	public TimeSpan NextFireTime;
	
	[ViewVariables]
	public MapId? TargetMapId = null;
}