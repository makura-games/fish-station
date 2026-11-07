using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;

namespace Content.Shared._Fish.Ipc;

/// <summary>
/// Старт и завершение сварки КПБ. Самосварка — использование зажжённой горелки в руке,
/// чужой ремонт — клик по шасси (серверный хук раньше Flammable).
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

        SubscribeLocalEvent<FishWeldingHealingComponent, UseInHandEvent>(OnUseInHand, before: [typeof(ItemToggleSystem)]);
        SubscribeLocalEvent<FishWeldingHealableComponent, FishSiliconRepairDoAfterEvent>(OnFinished);
    }

    private void OnUseInHand(Entity<FishWeldingHealingComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ItemToggleComponent>(ent, out var toggle) || !toggle.Activated)
            return;

        if (!ent.Comp.AllowSelfHeal)
            return;

        // Нет урона под сварку — не перехватываем, горелке можно погаснуть.
        if (TryStartWeld((ent, ent.Comp), args.User, args.User) != FishWeldResult.Started)
            return;

        args.Handled = true;
    }

    public FishWeldResult TryStartWeld(Entity<FishWeldingHealingComponent> tool, EntityUid user, EntityUid target)
    {
        if (!TryComp<FishWeldingHealableComponent>(target, out _)
            || !TryComp<InjurableComponent>(target, out var injurable)
            || injurable.DamageContainer is null
            || !tool.Comp.DamageContainers.Contains(injurable.DamageContainer.Value)
            || !_tool.HasQuality(tool, tool.Comp.QualityNeeded)
            || user == target && !tool.Comp.AllowSelfHeal)
            return FishWeldResult.Rejected;

        if (!HasDamage(target, tool.Comp))
            return FishWeldResult.NoDamage;

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
            if (!TryComp<WelderComponent>(used, out var welder)
                || !_solution.TryGetSolution(used, welder.FuelSolutionName, out var solution))
                return;

            _solution.RemoveReagent(solution.Value, welder.FuelReagent, healing.FuelCost);
        }
        else if (TryComp<StackComponent>(used, out var stack))
        {
            if (!_stack.TryUse((used, stack), healing.StackCost))
                return;
        }

        _damageable.TryChangeDamage(uid, healing.Damage, true, false, origin: args.User);
        _popup.PopupEntity(Loc.GetString("comp-repairable-repair", ("target", uid), ("tool", used)), uid, args.User);

        if (!HasDamage(uid, healing))
            return;

        var nextDelay = args.User == uid
            ? healing.DoAfterDelay * healing.SelfHealPenalty
            : healing.DoAfterDelay;

        _tool.UseTool(used, args.User, uid, nextDelay, healing.QualityNeeded, new FishSiliconRepairDoAfterEvent());
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
