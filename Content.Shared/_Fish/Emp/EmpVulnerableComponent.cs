using Content.Shared.Damage;

namespace Content.Shared._Fish.Emp;

/// <summary>
/// Наносит сущности урон при попадании под действие ЭМИ-импульса.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class EmpVulnerableComponent : Component
{
    /// <summary>
    /// Урон, наносимый сущности при срабатывании ЭМИ-импульса.
    /// </summary>
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Shock", 25f },
        },
    };

    /// <summary>
    /// Минимальный интервал между срабатываниями урона от ЭМИ.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.Zero;

    /// <summary>
    /// Время следующего допустимого получения урона от ЭМИ.
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextPulseTime;

    /// <summary>
    /// Помечать ли ЭМИ-событие как сработавшее для спавна визуального эффекта ЭМИ.
    /// </summary>
    [DataField]
    public bool TriggerEmpEffect = true;
}
