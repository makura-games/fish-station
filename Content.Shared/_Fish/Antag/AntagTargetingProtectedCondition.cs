using Content.Shared.EntityConditions;
using Content.Shared.EntityConditions.Conditions.Mind;
using Content.Shared.Mind;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Antag;

/// <summary>
/// Условие для PickFromPool: разум защищён от выбора целью антагонистов.
/// Обычно используется с <c>inverted: true</c>.
/// </summary>
public sealed partial class AntagTargetingProtectedCondition : EntityConditionBase<AntagTargetingProtectedCondition>
{
    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        return string.Empty;
    }
}

public sealed partial class AntagTargetingProtectedConditionSystem
    : EntityConditionSystem<MindComponent, AntagTargetingProtectedCondition>
{
    [Dependency] private SharedAntagTargetingProtectionSystem _protection = default!;

    protected override void Condition(Entity<MindComponent> entity, ref EntityConditionEvent<AntagTargetingProtectedCondition> args)
    {
        args.Result = _protection.IsProtected(entity, entity.Comp);
    }
}
