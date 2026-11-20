using Content.Shared.ActionBlocker;
using Content.Shared.Interaction;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Vehicle.Components;
using Content.Shared._Fish.Mech.Components;

namespace Content.Shared.Mech.EntitySystems;

public abstract partial class SharedMechSystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;

    // Допуск пилотов, ручные взаимодействия и дополнительные действия мехов Fish.
    private void InitializeFish()
    {
        SubscribeLocalEvent<MechComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
    }

    private void OnBeforeInteractHand(Entity<MechComponent> ent, ref BeforeInteractHandEvent args)
    {
        args.Handled = true;
    }

    /// <summary>
    /// Checks whether this mech allows a BorgBrain pilot to bypass the normal movement and whitelist requirements.
    /// </summary>
    protected bool CanInsertBrain(EntityUid mech, EntityUid entity)
    {
        return TryComp<MechBrainComponent>(mech, out var mechBrain) &&
               mechBrain.CanInsertBrain &&
               HasComp<BorgBrainComponent>(entity);
    }

    /// <summary>
    /// Checks if the pilot is present.
    /// </summary>
    public bool IsEmpty(Entity<MechComponent?> ent)
    {
        return !Vehicle.HasOperator(ent.Owner);
    }

    /// <summary>
    /// Checks if an entity can be inserted into the mech.
    /// </summary>
    public bool CanInsert(EntityUid uid, EntityUid toInsert, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        return CanInsertFishPilot((uid, component), toInsert);
    }

    /// <summary>
    /// Attempts to insert a pilot into the mech.
    /// </summary>
    public bool TryInsert(EntityUid uid, EntityUid? toInsert, MechComponent? component = null)
    {
        if (toInsert == null || !Resolve(uid, ref component))
            return false;

        if (!CanInsert(uid, toInsert.Value, component))
            return false;

        return DoInsertFishPilot((uid, component), toInsert.Value);
    }

    /// <summary>
    /// Attempts to eject the current pilot from the mech.
    /// </summary>
    public bool TryEject(EntityUid uid, MechComponent? component = null)
    {
        return Vehicle.TryExit(uid);
    }

    private bool CanInsertFishPilot(Entity<MechComponent> ent, EntityUid pilot)
    {
        if (ent.Comp.Broken || Vehicle.HasOperator(ent.Owner))
            return false;

        if (_whitelistSystem.IsWhitelistPass(ent.Comp.PilotBlacklist, pilot))
            return false;

        if (HasComp<BorgBrainComponent>(pilot))
        {
            if (!CanInsertBrain(ent, pilot))
                return false;
        }
        else if (!_actionBlocker.CanMove(pilot) || !Vehicle.CanOperate(ent.Owner, pilot))
        {
            return false;
        }

        return Vehicle.TryGetOperatorContainer(ent.Owner, out var container) &&
               _container.CanInsert(pilot, container);
    }

    private bool DoInsertFishPilot(Entity<MechComponent> ent, EntityUid pilot)
    {
        return Vehicle.TryGetOperatorContainer(ent.Owner, out var container) &&
               _container.Insert(pilot, container) &&
               Vehicle.GetOperatorOrNull(ent.Owner) == pilot;
    }

    private void UpdateFishBrainMovement(EntityUid pilot)
    {
        if (HasComp<BorgBrainComponent>(pilot))
            _actionBlocker.UpdateCanMove(pilot);
    }

    private void AddFishMechActions(EntityUid pilot, EntityUid mech, MechComponent component)
    {
        if (component.MechMassScannerAction is not { } action)
            return;

        _actions.AddAction(pilot, ref component.MechMassScannerActionEntity, action, mech);
    }
}
