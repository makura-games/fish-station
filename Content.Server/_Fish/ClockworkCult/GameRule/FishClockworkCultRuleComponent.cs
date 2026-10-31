using Content.Server._Fish.ClockworkCult.Systems;
using Content.Shared._Fish.ClockworkCult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.ClockworkCult.GameRule;

/// <summary>
/// Данные game rule Clockwork Cult.
/// </summary>
[RegisterComponent, Access(typeof(FishClockworkCultRuleSystem), typeof(FishClockworkServantSystem))]
public sealed partial class FishClockworkCultRuleComponent : Component
{
    [ViewVariables]
    public FishClockworkWinCondition WinCondition = FishClockworkWinCondition.Ongoing;

    [ViewVariables]
    public bool Ending;

    [ViewVariables]
    public HashSet<EntityUid> Servants = new();

    [ViewVariables]
    public EntityUid? ArkUid;

    [ViewVariables]
    public EntityUid? ReebeMapUid;

    [DataField]
    public EntProtoId MindRolePrototype = "FishMindRoleClockworkServant";

    [DataField]
    public EntProtoId StartingSlab = "FishClockworkSlab";

    [DataField]
    public SoundSpecifier GreetingSound =
        new SoundPathSpecifier("/Audio/_Fish/ClockworkCult/ambience/antag/clockcultalr.ogg");

    [DataField]
    public TimeSpan RoundEndDelay = TimeSpan.FromSeconds(20);
}
