using Content.Shared.ActionBlocker;
using Content.Shared.CombatMode;
using Content.Shared.Interaction.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;

namespace Content.Server._Fish.Mech;

public sealed partial class MechBrainCombatSystem : EntitySystem
{
    [Dependency] private SharedCombatModeSystem _combatMode = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BorgBrainComponent, OnVehicleEnteredEvent>(OnPilotEntered);
        SubscribeLocalEvent<MechBrainCombatComponent, ComponentShutdown>(OnPilotShutdown);
    }

    private void OnPilotEntered(Entity<BorgBrainComponent> ent, ref OnVehicleEnteredEvent args)
    {
        if (!HasComp<MechComponent>(args.Vehicle))
            return;

        var uid = ent.Owner;
        var hadCombatMode = HasComp<CombatModeComponent>(uid);
        var hadBlockMovement = TryComp<BlockMovementComponent>(uid, out var existingBlockMovement);
        var brainCombat = EnsureComp<MechBrainCombatComponent>(uid);
        brainCombat.AddedBlockMovement = !hadBlockMovement;
        brainCombat.BlockInteraction = existingBlockMovement?.BlockInteraction ?? true;
        brainCombat.AddedCombatMode = !hadCombatMode;
        brainCombat.AddedInputMover = !HasComp<InputMoverComponent>(uid);

        var blockMovement = existingBlockMovement ?? EnsureComp<BlockMovementComponent>(uid);
        blockMovement.BlockInteraction = false;
        Dirty(uid, blockMovement);

        // Контроллер нужен для передачи WASD и согласования камеры с мехом.
        EnsureComp<InputMoverComponent>(uid);

        // Компонент нужен для атак из меха, но установка мозга не включает боевой режим сама.
        var combatMode = EnsureComp<CombatModeComponent>(uid);
        brainCombat.InitialCombatMode = hadCombatMode && combatMode.IsInCombatMode;
    }


    private void OnPilotShutdown(Entity<MechBrainCombatComponent> ent, ref ComponentShutdown args)
    {
        RestoreBrainState(ent.Owner, ent.Comp, removeCombatComponent: false);
    }

    public void RestoreBrainState(EntityUid uid, MechBrainCombatComponent? brainCombat = null, bool removeCombatComponent = true)
    {
        if (LifeStage(uid) >= EntityLifeStage.Terminating)
            return;

        if (!Resolve(uid, ref brainCombat, false))
            return;

        if (TryComp<CombatModeComponent>(uid, out var combat))
        {
            _combatMode.SetInCombatMode(uid, brainCombat.InitialCombatMode, combat);

            if (brainCombat.AddedCombatMode)
                RemComp<CombatModeComponent>(uid);
        }

        if (brainCombat.AddedBlockMovement)
            RemComp<BlockMovementComponent>(uid);
        else if (TryComp<BlockMovementComponent>(uid, out var blockMovement))
        {
            blockMovement.BlockInteraction = brainCombat.BlockInteraction;
            Dirty(uid, blockMovement);
        }

        if (brainCombat.AddedInputMover)
            RemComp<InputMoverComponent>(uid);

        if (removeCombatComponent)
            RemComp<MechBrainCombatComponent>(uid);

        _actionBlocker.UpdateCanMove(uid);
    }
}
