using Content.Shared.Interaction;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared._Fish.Mech.Components;

namespace Content.Shared.Mech.EntitySystems;

public abstract partial class SharedMechSystem
{
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

    private bool CanInsertFishPilot(Entity<MechComponent> ent, EntityUid pilot)
    {
        if (ent.Comp.Broken || !IsEmpty(ent.Comp))
            return false;

        if (_whitelistSystem.IsWhitelistPass(ent.Comp.PilotBlacklist, pilot))
            return false;

        if (HasComp<BorgBrainComponent>(pilot))
        {
            if (!CanInsertBrain(ent, pilot))
                return false;
        }
        else if (!_actionBlocker.CanMove(pilot) || _whitelistSystem.IsWhitelistFail(ent.Comp.PilotWhitelist, pilot))
            return false;

        return _container.CanInsert(pilot, ent.Comp.PilotSlot);
    }

    private bool CanDragDropFishPilot(Entity<MechComponent> ent, EntityUid pilot)
    {
        return !HasComp<BorgBrainComponent>(pilot) && CanInsert(ent, pilot, ent.Comp);
    }

    private bool DoInsertFishPilot(Entity<MechComponent> ent, EntityUid pilot)
    {
        SetupUser(ent, pilot, ent.Comp);
        if (!_container.Insert(pilot, ent.Comp.PilotSlot))
        {
            // Вставка может быть отменена контейнером; временные компоненты пилота нужно снять.
            RemoveUser(ent, pilot);
            return false;
        }

        var ev = new MechSayEvent(ent, ent.Comp.MessageHello);
        RaiseLocalEvent(ent, ref ev, true);
        UpdateAppearance(ent, ent.Comp);
        return true;
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
