using Content.Shared.CombatMode;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;

namespace Content.Shared.Cuffs;

/// <summary>
/// Fish: успешный пуш (disarm) прерывает DoAfter самоосвобождения из наручников.
/// </summary>
public abstract partial class SharedCuffableSystem
{
    private void InitializeFishBreakoutPushInterrupt()
    {
        // После stamina/shove: только если пуш реально Handled (успех на сервере).
        SubscribeLocalEvent<CuffableComponent, DisarmedEvent>(
            OnFishDisarmedInterruptBreakout,
            after: [typeof(SharedStaminaSystem)]);
    }

    private void OnFishDisarmedInterruptBreakout(Entity<CuffableComponent> ent, ref DisarmedEvent args)
    {
        if (!args.Handled)
            return;

        if (_net.IsClient)
            return;

        TryInterruptSelfBreakout(ent, args.Source);
    }

    /// <summary>
    /// Прерывает активное самоосвобождение цели. Возвращает true, если DoAfter был отменён.
    /// </summary>
    public bool TryInterruptSelfBreakout(Entity<CuffableComponent> ent, EntityUid? source = null)
    {
        if (ent.Comp.SelfBreakoutDoAfter is not { } doAfterId)
            return false;

        if (!_doAfter.IsRunning(doAfterId))
        {
            ent.Comp.SelfBreakoutDoAfter = null;
            return false;
        }

        // Флаг читается в OnCuffableDoAfter (Cancel поднимает событие синхронно).
        ent.Comp.BreakoutInterruptedByPush = true;
        _doAfter.Cancel(doAfterId);
        ent.Comp.SelfBreakoutDoAfter = null;

        if (source is { Valid: true } pusher)
        {
            _popup.PopupEntity(
                Loc.GetString(
                    "fish-cuffable-breakout-interrupted-by-other",
                    ("target", Identity.Entity(ent, EntityManager))),
                ent,
                pusher);

            _adminLog.Add(
                LogType.Action,
                LogImpact.Medium,
                $"{ToPrettyString(pusher):player} interrupted handcuff breakout of {ToPrettyString(ent):subject} with a push");
        }

        return true;
    }
}
