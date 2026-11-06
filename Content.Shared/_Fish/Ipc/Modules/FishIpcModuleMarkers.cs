using Content.Shared.Damage;
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
}

/// <summary>Маркер хоста: ускоренная зарядка от ЛКП.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcApcAdapterHostComponent : Component
{
    [DataField]
    public float DrinkMultiplierBonus = 2f;

    [DataField]
    public float DrinkSpeedFactor = 0.55f;

    /// <summary>Сохранённый DrinkMultiplier до установки адаптера.</summary>
    [ViewVariables]
    public float? StoredDrinkMultiplier;

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
[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcPassiveHealModuleComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier HealPerSecond = new();
}

/// <summary>Модуль: выдаёт HUD/зрение на хост при установке.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcVisionModuleComponent : Component
{
    [DataField(required: true)]
    public FishIpcVisionKind Kind;
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

    [ViewVariables]
    public EntityUid? ActionEntity;
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

[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcMagneticJointsModuleComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcSealedJointsModuleComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcApcAdapterModuleComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcRadFilterModuleComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcVocalModModuleComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class FishIpcWireInterfaceModuleComponent : Component;
