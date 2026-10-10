using Content.Shared.Mech.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared._Fish.Mech.Components;

#pragma warning disable IDE0130 // Частичный класс расширяет ванильную систему в папке Fish.
namespace Content.Shared.Mech.EntitySystems;
#pragma warning restore IDE0130

public abstract partial class SharedMechSystem
{
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

    protected bool CanInsertBrain(EntityUid mech, EntityUid entity)
    {
        return TryComp<MechBrainComponent>(mech, out var mechBrain) &&
               mechBrain.CanInsertBrain &&
               HasComp<BorgBrainComponent>(entity);
    }
}
