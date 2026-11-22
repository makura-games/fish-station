using Content.Shared._Sunrise.TTS;
using Content.Shared.FixedPoint;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Trigger;

/// <summary>
/// Отправляет предупреждение в выбранный канал связи от имени сущности,
/// когда её суммарный урон достигает или превышает заданный порог.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class RattleOnDamageComponent : Component
{
    /// <summary>
    /// Каналы связи, в которые отправляется предупреждение.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<RadioChannelPrototype>> RadioChannels = new() { "Security" };

    /// <summary>
    /// Порог суммарного урона, при переходе которого отправляется сообщение.
    /// </summary>
    [DataField]
    public FixedPoint2 DamageThreshold = FixedPoint2.New(10);

    /// <summary>
    /// Ключ локализации сообщения. Поддерживает аргументы <c>$entity</c> и <c>$position</c>.
    /// </summary>
    [DataField]
    public LocId Message = "rattle-on-damage-breach-message";

    /// <summary>
    /// Опциональный звук сигнализации, проигрываемый при срабатывании предупреждения.
    /// По умолчанию отсутствует (<c>null</c>).
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound;

    /// <summary>
    /// Голос TTS для озвучивания радиооповещения. Если <c>null</c>, TTS для радио отключён.
    /// Используется только при отправке в радиоканал (не влияет на мессенджер).
    /// </summary>
    [DataField]
    public ProtoId<TTSVoicePrototype>? TtsVoice = "Portal2FactCore";

    /// <summary>
    /// Если <c>true</c>, сначала пытается отправить системное сообщение в группу мессенджера
    /// для указанного канала; если <c>false</c>, всегда отправляет в радиоканал от имени сущности.
    /// </summary>
    [DataField]
    public bool SendToMessenger;

    /// <summary>
    /// Сбрасывать ли состояние срабатывания, когда урон сущности снова опускается ниже порога.
    /// </summary>
    [DataField]
    public bool ResetOnHeal = true;

    /// <summary>
    /// Пропускать ли срабатывание, если у сущности есть <c>LockComponent</c>
    /// и она разблокирована (<c>Locked == false</c>).
    /// На сущностях без <c>LockComponent</c> не мешает срабатыванию.
    /// </summary>
    [DataField]
    public bool IgnoreWhenUnlocked = true;

    /// <summary>
    /// Минимальная задержка между повторными отправками предупреждения.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Было ли уже отправлено предупреждение после пересечения порога урона.
    /// </summary>
    [ViewVariables]
    public bool Triggered;

    /// <summary>
    /// Время следующей возможной отправки предупреждения.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan NextAlertTime = TimeSpan.Zero;
}
