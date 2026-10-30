using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Fish.Mining;

/// <summary>
/// Автоматически поглощает руду рядом с печью и ставит её в очередь переработки (lathe),
/// начисляя mining points (SalvageTicket) через существующий PrintTicket.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class OreRedemptionMachineComponent : Component
{
    /// <summary>
    /// Радиус поиска руды и рудных контейнеров (OreBox / сброшенный OreBag).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Range = 1.5f;

    /// <summary>
    /// Интервал сканирования области вокруг печи.
    /// </summary>
    [DataField]
    public TimeSpan ScanDelay = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextScan;

    /// <summary>
    /// После поглощения автоматически ставить в очередь рецепты OreProcessor.
    /// </summary>
    [DataField]
    public bool AutoProcess = true;

    /// <summary>
    /// Whitelist руды для прямого поглощения. Если null — используется whitelist MaterialStorage.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Максимум единиц руды, поглощаемых за один тик сканирования (защита от лагов).
    /// </summary>
    [DataField]
    public int MaxAbsorbPerScan = 24;

    /// <summary>
    /// Звук при успешном автопоглощении хотя бы одной руды.
    /// </summary>
    [DataField]
    public SoundSpecifier? AbsorbSound = new SoundPathSpecifier("/Audio/Machines/tray_eject.ogg");
}
