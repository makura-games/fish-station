using Content.Shared.Damage.Prototypes;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Medical.Healing;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Starlight.Medical.Items.Components;
using Content.Shared.Tools.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Ipc;

/// <summary>
/// Органическая медицина не лечит шасси КПБ и сообщает об этом.
/// Кабель и нанопаста проходят: их контейнеры урона включают Ipc.
/// </summary>
public sealed class FishIpcMedicineSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<DamageContainerPrototype>[] ChassisContainers =
    [
        "Ipc",
        "Silicon",
        "Synth",
        "Xenoborg",
        "Mech",
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HealingComponent, UseInHandEvent>(OnHealingUse, before: [typeof(HealingSystem)]);
        SubscribeLocalEvent<HealingComponent, AfterInteractEvent>(OnHealingAfterInteract, before: [typeof(HealingSystem)]);
        SubscribeLocalEvent<PatchComponent, UseInHandEvent>(OnPatchUse, before: [typeof(Content.Shared.Medical.Items.EntitySystems.PatchSystem)]);
        SubscribeLocalEvent<PatchComponent, AfterInteractEvent>(OnPatchAfterInteract, before: [typeof(Content.Shared.Medical.Items.EntitySystems.PatchSystem)]);
        SubscribeLocalEvent<InjectorComponent, UseInHandEvent>(OnInjectorUse, before: [typeof(InjectorSystem)]);
        SubscribeLocalEvent<InjectorComponent, AfterInteractEvent>(OnInjectorAfterInteract, before: [typeof(InjectorSystem)]);
        SubscribeLocalEvent<EdibleComponent, UseInHandEvent>(OnEdibleUse, after: [typeof(OpenableSystem)], before: [typeof(IngestionSystem)]);
        SubscribeLocalEvent<EdibleComponent, AfterInteractEvent>(OnEdibleAfterInteract, after: [typeof(ToolOpenableSystem)], before: [typeof(IngestionSystem)]);
        SubscribeLocalEvent<FishIpcComponent, SolutionTransferAttemptEvent>(OnSolutionTransferAttempt);
    }

    private void OnHealingUse(Entity<HealingComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || HealsChassis(ent.Comp) || !IsIpc(args.User))
            return;

        Reject(args.User, args.User);
        args.Handled = true;
    }

    private void OnHealingAfterInteract(Entity<HealingComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (HealsChassis(ent.Comp) || !IsIpc(target))
            return;

        Reject(target, args.User);
        args.Handled = true;
    }

    private void OnPatchUse(Entity<PatchComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !IsIpc(args.User))
            return;

        Reject(args.User, args.User);
        args.Handled = true;
    }

    private void OnPatchAfterInteract(Entity<PatchComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !IsIpc(target))
            return;

        Reject(target, args.User);
        args.Handled = true;
    }

    private void OnInjectorUse(Entity<InjectorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !IsIpc(args.User))
            return;

        if (!_proto.TryIndex(ent.Comp.ActiveModeProtoId, out var mode) || !mode.InjectOnUse)
            return;

        Reject(args.User, args.User);
        args.Handled = true;
    }

    private void OnInjectorAfterInteract(Entity<InjectorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !IsIpc(target))
            return;

        Reject(target, args.User);
        args.Handled = true;
    }

    private void OnEdibleUse(Entity<EdibleComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !IsIpc(args.User))
            return;

        Reject(args.User, args.User);
        args.Handled = true;
    }

    private void OnEdibleAfterInteract(Entity<EdibleComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !IsIpc(target))
            return;

        Reject(target, args.User);
        args.Handled = true;
    }

    private void OnSolutionTransferAttempt(Entity<FishIpcComponent> ent, ref SolutionTransferAttemptEvent args)
    {
        if (args.To != ent.Owner || args.CancelReason != null)
            return;

        args.Cancel(Loc.GetString("fish-ipc-not-organic"));
    }

    private void Reject(EntityUid target, EntityUid user)
    {
        _popup.PopupPredicted(Loc.GetString("fish-ipc-not-organic"), target, user, PopupType.SmallCaution);
    }

    private bool IsIpc(EntityUid uid)
    {
        return HasComp<FishIpcComponent>(uid);
    }

    private static bool HealsChassis(HealingComponent healing)
    {
        if (healing.DamageContainers is not { Count: > 0 } containers)
            return true;

        foreach (var container in containers)
        {
            foreach (var chassis in ChassisContainers)
            {
                if (container == chassis)
                    return true;
            }
        }

        return false;
    }
}
