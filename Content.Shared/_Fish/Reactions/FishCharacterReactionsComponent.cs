using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Fish.Reactions;

/// <summary>
/// Включает набор физических реакций персонажа (боль, кровь, огонь, взрывы в руках)
/// и их связку с Crit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedFishCharacterReactionsSystem))]
public sealed partial class FishCharacterReactionsComponent : Component
{
    /// <summary>Шанс детонации взрывчатки в руке от пули.</summary>
    [DataField, AutoNetworkedField]
    public float HeldExplosiveDetonateChance = 0.15f;

    /// <summary>Минимальный прирост урона для реакции на выстрел по гранате.</summary>
    [DataField, AutoNetworkedField]
    public float HeldExplosiveMinDamage = 1f;

    /// <summary>Кулдаун кровоточащего следа.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan BleedTrailCooldown = TimeSpan.FromSeconds(0.6);

    /// <summary>Серверный таймер следующего кровоточащего следа (не сетевой).</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextBleedTrail;

    /// <summary>Сколько крови в раствор ног/тела за тик следа.</summary>
    [DataField, AutoNetworkedField]
    public float BleedTrailTransfer = 2.5f;

    /// <summary>Минимум крови, проливаемой на пол при попадании (лужа).</summary>
    [DataField, AutoNetworkedField]
    public float HitBloodSpill = 2.5f;

    /// <summary>Сколько крови наносить на ноги/тело при попадании (для следов).</summary>
    [DataField, AutoNetworkedField]
    public float HitBloodOnSurface = 4f;

    /// <summary>Минимальный прирост кровоточащего урона для брызг/следов.</summary>
    [DataField, AutoNetworkedField]
    public float HitBloodMinDamage = 3f;

    /// <summary>Серверный таймер следующего болевого звука (не сетевой).</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextPainSound;

    /// <summary>Кулдаун болевого крика (только звук, без эмоута).</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan PainSoundCooldown = TimeSpan.FromSeconds(4.5);

    /// <summary>Минимальный «реальный» урон (без asphyx/poison-only) для крика.</summary>
    [DataField, AutoNetworkedField]
    public float PainMinDamage = 8f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier MalePainSound =
        new SoundCollectionSpecifier("MaleScreams", AudioParams.Default.WithVolume(2f).WithVariation(0.08f));

    [DataField, AutoNetworkedField]
    public SoundSpecifier FemalePainSound =
        new SoundCollectionSpecifier("FemaleScreams", AudioParams.Default.WithVolume(2f).WithVariation(0.08f));

    [DataField, AutoNetworkedField]
    public SoundSpecifier BleedSound =
        new SoundCollectionSpecifier("FishCritBleed", AudioParams.Default.WithVolume(-4f).WithVariation(0.15f));

    [DataField, AutoNetworkedField]
    public SoundSpecifier BurnSound =
        new SoundCollectionSpecifier("FishCritBurn", AudioParams.Default.WithVolume(-2f));

    [DataField, AutoNetworkedField]
    public TimeSpan PainJitterDuration = TimeSpan.FromSeconds(1.25);

    [DataField, AutoNetworkedField]
    public float PainJitterAmplitude = 10f;
}
