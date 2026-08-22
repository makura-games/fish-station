using Content.Shared.Explosion;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.BluespaceArtillery;

/// <summary>
/// Ручной лазерный целеуказатель блюспейс-артиллерии (ЛЦУ БСА).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedBluespaceArtilleryDesignatorSystem))]
public sealed partial class BluespaceArtilleryDesignatorComponent : Component
{
    /// <summary>
    /// Задержка наведения/подтверждения цели перед запуском удара.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan TargetingDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Задержка между подтверждением удара и взрывом.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan StrikeDelay = TimeSpan.FromSeconds(7);

    /// <summary>
    /// Визуальная метка цели на время отсчёта до удара.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId TargetMarker = "EffectBluespaceArtilleryTarget";

    [DataField]
    public LocId Announcement = "bluespace-artillery-announcement";

    [DataField]
    public LocId AnnouncementSender = "bluespace-artillery-announcement-sender";

    [DataField]
    public Color AnnouncementColor = Color.FromHex("#0000FF");

    [DataField]
    public SoundSpecifier AnnouncementSound = new SoundPathSpecifier(
        "/Audio/_Sunrise/Announcements/sunrise_artillery.ogg",
        AudioParams.Default.WithVolume(10f));

    [DataField(required: true), AutoNetworkedField]
    public List<BluespaceArtilleryFireMode> FireModes = new();

    [DataField, AutoNetworkedField]
    public int CurrentFireMode;
}

/// <summary>
/// Параметры одного режима залпа ЛЦУ БСА для существующей ExplosionSystem.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class BluespaceArtilleryFireMode
{
    [DataField(required: true)]
    public LocId Name = string.Empty;

    [DataField(required: true)]
    public ProtoId<ExplosionPrototype> ExplosionType;

    [DataField(required: true)]
    public float TotalIntensity;

    [DataField(required: true)]
    public float IntensitySlope;

    [DataField(required: true)]
    public float MaxIntensity;

    [DataField]
    public float TileBreakScale = 1f;

    [DataField]
    public int MaxTileBreak = int.MaxValue;

    [DataField]
    public bool CanCreateVacuum = true;
}
