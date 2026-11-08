using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Fish.Ipc.Modules;

public enum FishIpcVisionKind : byte
{
    Medical,
    Security,
    Meson,
    NightVision,
}

/// <summary>Маркер хоста: частичная EMP-защита шасси.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcEmpScreenHostComponent : Component
{
    /// <summary>Множитель энергии EMP-урона (0.4 = −60%).</summary>
    [DataField]
    public float DamageMultiplier = 0.4f;

    /// <summary>Не выставлять Disabled на EmpPulse при установленном экране.</summary>
    [DataField]
    public bool BlockDisable = true;
}

/// <summary>Маркер хоста: магнитные суставы (сложнее ампутировать, EMP глушит).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishIpcMagneticJointsHostComponent : Component
{
    /// <summary>Шанс отменить ампутацию (0–1), пока эффект активен.</summary>
    [DataField]
    public float ResistChance = 0.65f;

    [DataField, AutoNetworkedField]
    public bool EmpDisabled;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan EmpDisabledUntil;

    /// <summary>Выключено action-тогглом модуля.</summary>
    [DataField, AutoNetworkedField]
    public bool ModuleEnabled = true;
}

/// <summary>Маркер хоста: герметичные суставы (блокируют ампутацию, EMP = долгий стан).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishIpcSealedJointsHostComponent : Component
{
    [DataField]
    public TimeSpan EmpStunDuration = TimeSpan.FromSeconds(12);

    [DataField, AutoNetworkedField]
    public bool EmpDisabled;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan EmpDisabledUntil;

    [DataField, AutoNetworkedField]
    public bool ModuleEnabled = true;
}

/// <summary>Маркер хоста: ускоренная зарядка от ЛКП.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcApcAdapterHostComponent : Component
{
    /// <summary>Ускорение do-after (множитель времени).</summary>
    [DataField]
    public float DrinkSpeedFactor = 0.55f;

    [ViewVariables]
    public float? StoredDrinkSpeed;
}

/// <summary>Маркер хоста: фильтр радиации/мозгового урона.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcRadFilterHostComponent : Component
{
    /// <summary>Множитель Radiation / Cellular / Poison.</summary>
    [DataField]
    public float Coefficient = 0.35f;
}

/// <summary>Маркер хоста: вокальный модулятор — рация работает на низком заряде.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcVocalModHostComponent : Component;

/// <summary>Маркер хоста: интерфейс проводов — доп. examine панелей.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcWireInterfaceHostComponent : Component;

/// <summary>Модуль пассивного хила (наноремонт / реактивный).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishIpcPassiveHealModuleComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier HealPerSecond = new();

    [DataField]
    public EntProtoId ToggleAction = "FishActionIpcToggleRepair";

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [ViewVariables]
    public EntityUid? ToggleActionEntity;

    /// <summary>Накопитель дробного хила между тиками FixedPoint2.</summary>
    [ViewVariables]
    public Dictionary<string, FixedPoint2> Accumulator = new();
}

/// <summary>Модуль: выдаёт HUD/зрение на хост при установке.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishIpcVisionModuleComponent : Component
{
    [DataField(required: true)]
    public FishIpcVisionKind Kind;

    [DataField]
    public EntProtoId ToggleAction = "FishActionIpcToggleVision";

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [ViewVariables]
    public EntityUid? ToggleActionEntity;
}

/// <summary>Модуль встроенного дефибриллятора.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcDefibModuleComponent : Component
{
    [DataField]
    public EntProtoId Action = "FishActionIpcDefib";

    [DataField]
    public float ChargeFraction = 0.4f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(90);

    [DataField]
    public TimeSpan DoAfter = TimeSpan.FromSeconds(4);

    [DataField]
    public DamageSpecifier ZapHeal = new()
    {
        DamageDict = { ["Asphyxiation"] = -40 }
    };

    [DataField]
    public SoundSpecifier? ChargeSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_charge.ogg");

    [DataField]
    public SoundSpecifier? ZapSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_zap.ogg");

    [DataField]
    public SoundSpecifier? SuccessSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_success.ogg");

    [DataField]
    public SoundSpecifier? FailureSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_failed.ogg");

    [DataField]
    public EntProtoId PadsPrototype = "FishIpcDefibPads";

    [ViewVariables]
    public EntityUid? ActionEntity;

    [ViewVariables]
    public EntityUid? PadsEntity;

    [ViewVariables]
    public bool PadsActive;
}

/// <summary>Виртуальные/реальные накладки дефиба в руках КПБ.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcDefibPadsComponent : Component
{
    [DataField]
    public EntityUid? Module;
}

/// <summary>Модуль jumpstarter — выход из crit.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcJumpstarterModuleComponent : Component
{
    [DataField]
    public EntProtoId Action = "FishActionIpcJumpstarter";

    [DataField]
    public float ChargeFraction = 0.3f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(180);

    [DataField]
    public DamageSpecifier HealOnUse = new()
    {
        DamageDict =
        {
            ["Blunt"] = -15,
            ["Slash"] = -10,
            ["Piercing"] = -10,
            ["Heat"] = -10,
            ["Shock"] = -10,
        }
    };

    [ViewVariables]
    public EntityUid? ActionEntity;
}

/// <summary>Модуль инструмент-рука: спавнит один тул в руку.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcToolHandModuleComponent : Component
{
    [DataField]
    public EntProtoId ToolPrototype = "Crowbar";

    [ViewVariables]
    public EntityUid? SpawnedTool;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcEmpScreenModuleComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishIpcMagneticJointsModuleComponent : Component
{
    [DataField]
    public EntProtoId ToggleAction = "FishActionIpcToggleMagneticJoints";

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [ViewVariables]
    public EntityUid? ToggleActionEntity;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishIpcSealedJointsModuleComponent : Component
{
    [DataField]
    public EntProtoId ToggleAction = "FishActionIpcToggleSealedJoints";

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [ViewVariables]
    public EntityUid? ToggleActionEntity;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcApcAdapterModuleComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcRadFilterModuleComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcVocalModModuleComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcWireInterfaceModuleComponent : Component;

/// <summary>
/// Кнопка вкл/выкл для модулей без собственного тумблера.
/// Выключенный модуль не даёт эффект и не тратит заряд.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishIpcModulePowerToggleComponent : Component
{
    [DataField]
    public EntProtoId ToggleAction = "FishActionIpcToggleModule";

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [ViewVariables]
    public EntityUid? ToggleActionEntity;
}
