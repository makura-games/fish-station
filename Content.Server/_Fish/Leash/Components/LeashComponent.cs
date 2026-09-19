using Content.Shared.Damage;

namespace Content.Server._Fish.Leash.Components;

[RegisterComponent, Access(typeof(Systems.LeashSystem))]
public sealed partial class LeashComponent : Component
{
    [DataField]
    public DamageSpecifier ChokeDamage = new()
    {
        DamageDict =
        {
            ["Asphyxiation"] = 0.75,
        },
    };

    [DataField]
    public TimeSpan ChokeCooldown = TimeSpan.FromSeconds(0.5);

    [DataField]
    public float ChokeDistance = 3.25f;

    [DataField]
    public float ResistanceThreshold = 0.05f;

    [DataField]
    public float MaxTensionDistance = 1.35f;

    [DataField]
    public float MaximumDistance = 4f;

    [DataField]
    public float PullForce = 32f;

    [DataField("modes")]
    public List<float> Modes = new() { 1f, 2.5f, 4f, 5.5f, 7f };

    [DataField("currentModeIndex")]
    public int CurrentModeIndex = 2;

    public EntityUid? AttachedCollar;
    public EntityUid? Holder;
    public TimeSpan NextChokeTime;
}
