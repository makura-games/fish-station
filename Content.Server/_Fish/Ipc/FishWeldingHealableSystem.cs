using Content.Shared._Fish.Ipc;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;

namespace Content.Server._Fish.Ipc;

/// <summary>Ремонт КПБ сваркой (Paradise: welder → burn).</summary>
public sealed partial class FishWeldingHealableSystem : EntitySystem
{
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishWeldingHealableComponent, InteractUsingEvent>(OnInteract);
        SubscribeLocalEvent<FishWeldingHealableComponent, FishSiliconRepairDoAfterEvent>(OnFinished);
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

        args.Handled = _tool.UseTool(used, args.User, uid, 3f, healing.QualityNeeded, new FishSiliconRepairDoAfterEvent());
    }

    private void OnInteract(EntityUid uid, FishWeldingHealableComponent _, InteractUsingEvent args)
    {
        if (args.Handled
            || !TryComp(args.Used, out FishWeldingHealingComponent? healing)
            || !TryComp(args.Target, out InjurableComponent? injurable)
            || injurable.DamageContainer is null
            || !healing.DamageContainers.Contains(injurable.DamageContainer.Value)
            || !HasDamage(args.Target, healing)
            || !_tool.HasQuality(args.Used, healing.QualityNeeded)
            || args.User == args.Target && !healing.AllowSelfHeal)
            return;

        var delay = args.User == args.Target
            ? healing.DoAfterDelay * healing.SelfHealPenalty
            : healing.DoAfterDelay;

        args.Handled = _tool.UseTool(args.Used, args.User, args.Target, delay, healing.QualityNeeded,
            new FishSiliconRepairDoAfterEvent());
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
