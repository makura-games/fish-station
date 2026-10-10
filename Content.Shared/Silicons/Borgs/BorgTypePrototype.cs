// Fish-edit
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Radio;
using Content.Shared.Roles;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
// Fish-edit
using System.Numerics;
// Fish-edit
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
// Fish-edit
using Robust.Shared.Utility;

namespace Content.Shared.Silicons.Borgs;

/// <summary>
/// Information for a borg type that can be selected by <see cref="BorgSwitchableTypeComponent"/>.
/// </summary>
/// <seealso cref="SharedBorgSwitchableTypeSystem"/>
[Prototype]
public sealed partial class BorgTypePrototype : IPrototype, IInheritingPrototype
{
    private static readonly ProtoId<SoundCollectionPrototype> DefaultFootsteps = new("FootstepBorg");

    [IdDataField]
    public required string ID { get; set; }

    // Fish-Start
    // Поддержка parent: для borgType (Mk2-варианты поверх существующих специализаций).
    /// <inheritdoc/>
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<BorgTypePrototype>))]
    public string[]? Parents { get; private set; }

    /// <inheritdoc />
    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }
    // Fish-End

    //
    // Description info (name/desc) is configured via localization strings directly.
    //

    /// <summary>
    /// The prototype displayed in the selection menu for this type.
    /// </summary>
    [DataField]
    public required EntProtoId DummyPrototype;

    // Fish-Start
    // Скрытие типа из меню выбора специализации.
    /// <summary>
    /// Не показывать тип в меню выбора специализации.
    /// Используется для Mk2-вариантов, скрытых от ручного выбора.
    /// </summary>
    [DataField]
    public bool HideInMenu;
    // Fish-End

    //
    // Functional information
    //

    /// <summary>
    /// The amount of free module slots this borg type has.
    /// </summary>
    /// <remarks>
    /// This count is on top of the modules specified in <see cref="DefaultModules"/>.
    /// </remarks>
    /// <seealso cref="BorgChassisComponent.ModuleCount"/>
    [DataField]
    public int ExtraModuleCount { get; set; } = 0;

    /// <summary>
    /// The whitelist for borg modules that can be inserted into this borg type.
    /// </summary>
    /// <seealso cref="BorgChassisComponent.ModuleWhitelist"/>
    [DataField]
    public EntityWhitelist? ModuleWhitelist { get; set; }

    /// <summary>
    /// Inventory template used by this borg.
    /// </summary>
    /// <remarks>
    /// This template must be compatible with the normal borg templates,
    /// so in practice it can only be used to differentiate the visual position of the slots on the character sprites.
    /// </remarks>
    /// <seealso cref="InventorySystem.SetTemplateId"/>
    [DataField]
    public ProtoId<InventoryTemplatePrototype> InventoryTemplateId { get; set; } = "borgShort";

    /// <summary>
    /// Radio channels that this borg will gain access to from this module.
    /// </summary>
    /// <remarks>
    /// These channels are provided on top of the ones specified in
    /// <see cref="BorgSwitchableTypeComponent.InherentRadioChannels"/>.
    /// </remarks>
    [DataField]
    public ProtoId<RadioChannelPrototype>[] RadioChannels = [];

    /// <summary>
    /// Borg module types that are always available to borgs of this type.
    /// </summary>
    /// <remarks>
    /// These modules still work like modules, although they cannot be removed from the borg.
    /// </remarks>
    /// <seealso cref="BorgModuleComponent.DefaultModule"/>
    [DataField]
    public EntProtoId[] DefaultModules = [];

    /// <summary>
    /// Additional components to add to the borg entity when this type is selected.
    /// </summary>
    [DataField]
    public ComponentRegistry? AddComponents { get; set; }

    //
    // Visual information
    //

    /// <summary>
    /// The sprite state for the main borg body.
    /// </summary>
    [DataField]
    public string SpriteBodyState { get; set; } = "robot";

    /// <summary>
    /// An optional movement sprite state for the main borg body.
    /// </summary>
    [DataField]
    public string? SpriteBodyMovementState { get; set; }

    /// <summary>
    /// Sprite state used to indicate that the borg has a mind in it.
    /// </summary>
    /// <seealso cref="BorgChassisComponent.HasMindState"/>
    [DataField]
    public string SpriteHasMindState { get; set; } = "robot_e";

    /// <summary>
    /// Sprite state used to indicate that the borg has no mind in it.
    /// </summary>
    /// <seealso cref="BorgChassisComponent.NoMindState"/>
    [DataField]
    public string SpriteNoMindState { get; set; } = "robot_e_r";

    /// <summary>
    /// Sprite state used when the borg's flashlight is on.
    /// </summary>
    [DataField]
    public string SpriteToggleLightState { get; set; } = "robot_l";

    // Fish-Start
    // Отдельный холст спрайта для типов, чей RSI не совпадает с RSI chassis сущности.
    // Нужен Mk2: у них корпус рисуется из _Lust/Mobs/Silicon/chassis.rsi, а живая сущность
    // осталась на Mobs/Silicon/chassis.rsi от родительского chassis. Без этого поля
    // смена borgType меняла бы только state и оставляла старый холст (а у security
    // state sec_mk2* на vanilla-RSI вообще отсутствует).
    /// <summary>
    /// RSI, из которого берутся все слои спрайта. <c>null</c> — RSI не трогаем.
    /// </summary>
    [DataField]
    public ResPath? SpriteRsiPath;

    /// <summary>
    /// Смещение слоёв спрайта в тайлах. <c>null</c> — смещение не трогаем.
    /// Для _Lust (холст 32x64 против vanilla 32x32) нужно 0,0.5, иначе борг висит на полтайла ниже.
    /// Вешается на корень SpriteComponent, а не на отдельные слои: так runtime-слои
    /// предметов (in-hand, надетая экипировка) наследуют сдвиг и едут вместе с телом.
    /// </summary>
    [DataField]
    public Vector2? SpriteOffset;
    // Fish-End

    //
    // Minor information
    //

    /// <summary>
    /// String to use on petting success.
    /// </summary>
    /// <seealso cref="InteractionPopupComponent"/>
    [DataField]
    public string PetSuccessString { get; set; } = "petting-success-generic-cyborg";

    /// <summary>
    /// String to use on petting failure.
    /// </summary>
    /// <seealso cref="InteractionPopupComponent"/>
    [DataField]
    public string PetFailureString { get; set; } = "petting-failure-generic-cyborg";

    //
    // Sounds
    //

    /// <summary>
    /// Sound specifier for footstep sounds created by this borg.
    /// </summary>
    [DataField]
    public SoundSpecifier FootstepCollection { get; set; } = new SoundCollectionSpecifier(DefaultFootsteps);

    // Sunrise-Start
    [DataField(required: true, customTypeSerializer: typeof(PrototypeIdSerializer<JobPrototype>))]
    public string Job = string.Empty;
    // Sunrise-End
}
