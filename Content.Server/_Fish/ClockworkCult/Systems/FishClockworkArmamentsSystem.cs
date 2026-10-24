using Content.Shared._Fish.ClockworkCult.Components;
using Content.Shared._Fish.ClockworkCult.Events;
using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.ClockworkCult.Systems;

/// <summary>
/// Выдача CallArmor / CallSpear actions при добавлении FishClockworkArmamentsComponent.
/// </summary>
public sealed partial class FishClockworkArmamentsSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private FishClockworkScriptureSystem _scripture = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishClockworkArmamentsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FishClockworkArmamentsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FishClockworkArmamentsComponent, FishClockworkCallArmorActionEvent>(OnCallArmor);
        SubscribeLocalEvent<FishClockworkArmamentsComponent, FishClockworkCallSpearActionEvent>(OnCallSpear);
    }

    private void OnStartup(Entity<FishClockworkArmamentsComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.CallArmorActionEntity == null)
            _actions.AddAction(ent.Owner, ref ent.Comp.CallArmorActionEntity, ent.Comp.CallArmorAction);

        if (ent.Comp.CallSpearActionEntity == null)
            _actions.AddAction(ent.Owner, ref ent.Comp.CallSpearActionEntity, ent.Comp.CallSpearAction);

        Dirty(ent);
    }

    private void OnShutdown(Entity<FishClockworkArmamentsComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Comp.CallArmorActionEntity);
        _actions.RemoveAction(ent.Comp.CallSpearActionEntity);
    }

    private void OnCallArmor(Entity<FishClockworkArmamentsComponent> ent, ref FishClockworkCallArmorActionEvent args)
    {
        if (args.Handled)
            return;

        if (_scripture.TryEquipArmaments(ent.Owner))
            args.Handled = true;
    }

    private void OnCallSpear(Entity<FishClockworkArmamentsComponent> ent, ref FishClockworkCallSpearActionEvent args)
    {
        if (args.Handled)
            return;

        if (_scripture.TryCallSpear(ent.Owner))
            args.Handled = true;
    }
}
