using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;

namespace Content.Shared._Fish.Ipc;

/// <summary>
/// Старт и завершение сварки КПБ. Клик зажжённой горелкой по шасси.
/// Использование в руке не перехватываем: Z всегда гасит горелку.
/// </summary>
public sealed partial class SharedFishWeldingHealableSystem : EntitySystem
{
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishWeldingHealableComponent, FishSiliconRepairDoAfterEvent>(OnFinished);
    }

    public FishWeldResult TryStartWeld(Entity<FishWeldingHealingComponent> tool, EntityUid user, EntityUid target)
    {
        if (!TryComp<FishWeldingHealableComponent>(target, out _)
            || !TryComp<ItemToggleComponent>(tool, out var toggle)
            || !toggle.Activated
            || !TryComp<InjurableComponent>(target, out var injurable)
            || injurable.DamageContainer is null
            || !tool.Comp.DamageContainers.Contains(injurable.DamageContainer.Value)
            || !_tool.HasQuality(tool, tool.Comp.QualityNeeded)
            || user == target && !tool.Comp.AllowSelfHeal)
            return FishWeldResult.Rejected;

        if (!HasDamage(target, tool.Comp))
            return FishWeldResult.NoDamage;

        if (!WelderCanPay(tool, tool.Comp))
            return FishWeldResult.Rejected;

        var delay = user == target
            ? tool.Comp.DoAfterDelay * tool.Comp.SelfHealPenalty
            : tool.Comp.DoAfterDelay;

        var started = _tool.UseTool(tool, user, target, delay, tool.Comp.QualityNeeded, new FishSiliconRepairDoAfterEvent());
        return started ? FishWeldResult.Started : FishWeldResult.Rejected;
    }

    private void OnFinished(EntityUid uid, FishWeldingHealableComponent _, FishSiliconRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Used is not { } used
            || !TryComp<InjurableComponent>(args.Target, out var injurable)
            || !TryComp<FishWeldingHealingComponent>(used, out var healing)
            || injurable.DamageContainer is null
            || !healing.DamageContainers.Contains(injurable.DamageContainer.Value)
            || !HasDamage(uid, healing))
            return;

        if (healing.RequireWelderFuel)
        {
            if (!TryPayWelder(used, healing))
                return;
        }
        else if (TryComp<StackComponent>(used, out var stack))
        {
            if (!_stack.TryUse((used, stack), healing.StackCost))
                return;
        }

        _damageable.TryChangeDamage(uid, healing.Damage, ignoreResistances: true, interruptsDoAfters: false, origin: args.User, ignoreVariance: true);
        _popup.PopupEntity(Loc.GetString("comp-repairable-repair", ("target", uid), ("tool", used)), uid, args.User);

        if (!HasDamage(uid, healing) || !WelderCanPay(used, healing))
            return;

        var nextDelay = args.User == uid
            ? healing.DoAfterDelay * healing.SelfHealPenalty
            : healing.DoAfterDelay;

        _tool.UseTool(used, args.User, uid, nextDelay, healing.QualityNeeded, new FishSiliconRepairDoAfterEvent());
    }

    /// <summary>Горелка горит и топлива хватает на один проход. Иначе лечение не стартует и не повторяется.</summary>
    private bool WelderCanPay(EntityUid tool, FishWeldingHealingComponent healing)
    {
        if (!healing.RequireWelderFuel)
            return true;

        if (!TryComp<ItemToggleComponent>(tool, out var toggle) || !toggle.Activated)
            return false;

        return TryGetWelderFuel(tool, out var solutionEnt, out var welder)
            && solutionEnt.Comp.Solution.GetTotalPrototypeQuantity(welder.FuelReagent) >= healing.FuelCost;
    }

    private bool TryPayWelder(EntityUid tool, FishWeldingHealingComponent healing)
    {
        if (!WelderCanPay(tool, healing) || !TryGetWelderFuel(tool, out var solutionEnt, out var welder))
            return false;

        var removed = _solution.RemoveReagent(solutionEnt, welder.FuelReagent, healing.FuelCost);
        return removed >= healing.FuelCost;
    }

    private bool TryGetWelderFuel(EntityUid tool, out Entity<SolutionComponent> solutionEnt, out WelderComponent welder)
    {
        solutionEnt = default;
        if (!TryComp(tool, out welder!)
            || !_solution.TryGetSolution(tool, welder.FuelSolutionName, out var resolved))
            return false;

        solutionEnt = resolved.Value;
        return true;
    }

    private bool HasDamage(EntityUid target, FishWeldingHealingComponent healing)
    {
        var current = _damageable.GetAllDamage(target);
        foreach (var (type, _) in healing.Damage.DamageDict)
        {
            if (current.DamageDict.TryGetValue(type, out var amount) && amount > 0)
                return true;
        }

        return false;
    }
}

public enum FishWeldResult : byte
{
    Rejected,
    NoDamage,
    Started,
}
