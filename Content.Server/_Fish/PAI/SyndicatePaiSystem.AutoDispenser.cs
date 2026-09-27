using Content.Shared._Fish.PAI;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._Fish.PAI;

public sealed partial class SyndicatePaiSystem
{
    [Dependency] private MobThresholdSystem _mobThresholds = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedInteractionSystem _serverInteraction = default!;
    [Dependency] private SharedSolutionContainerSystem _serverSolutions = default!;

    private TimeSpan _nextAutoCheck;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTiming.CurTime < _nextAutoCheck)
            return;

        _nextAutoCheck = _gameTiming.CurTime + TimeSpan.FromSeconds(1);
        ProcessAutoDispensers();
    }

    private void ProcessAutoDispensers()
    {
        var query = EntityQueryEnumerator<SyndicatePaiComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.AutoDispenserUnlocked || !comp.AutoDispenserEnabled)
                continue;

            TryAutoInject((uid, comp));
        }
    }

    /// <summary>
    /// Автоинъекция только владельцу при низком здоровье; кулдаун 10 минут.
    /// </summary>
    private void TryAutoInject(Entity<SyndicatePaiComponent> ent)
    {
        if (_gameTiming.CurTime < ent.Comp.NextAutoInjectTime)
            return;

        if (!TryGetOwnerTarget(ent, out var owner) || owner == null)
            return;

        // Только пока пИИ в вещах владельца (руки / инвентарь / КПК)
        if (!IsHeldByOwner(ent, owner.Value))
            return;

        if (!_mobState.IsAlive(owner.Value))
            return;

        if (!TryComp<DamageableComponent>(owner.Value, out var damageable))
            return;

        if (!_mobThresholds.TryGetThresholdForState(owner.Value, MobState.Critical, out var critThreshold) ||
            critThreshold <= FixedPoint2.Zero)
            return;

        var healthPercent = (1f - (_damageable.GetTotalDamage((owner.Value, damageable)) / critThreshold.Value).Float()) * 100f;
        if (healthPercent > ent.Comp.AutoHealthThreshold)
            return;

        if (!TryGetAutoHypo(ent, out var hypo) || hypo == null)
            return;

        if (!TryComp<SolutionRegenerationComponent>(hypo.Value, out _) ||
            !TryGetHypoSolution(hypo.Value, out _, out var solution))
            return;

        // Экстренный гипо вводит весь резервуар (CurrentTransferAmount = null)
        if (solution.Volume <= FixedPoint2.Zero)
            return;

        _serverInteraction.InteractUsing(
            ent.Owner,
            hypo.Value,
            owner.Value,
            Transform(owner.Value).Coordinates,
            checkCanInteract: false,
            checkCanUse: false,
            needHand: false);

        ent.Comp.NextAutoInjectTime = _gameTiming.CurTime + ent.Comp.AutoInjectCooldown;
        Dirty(ent);
        UpdateUiState(ent);

        _serverPopup.PopupEntity(
            Loc.GetString("syndicate-pai-auto-injected", ("owner", Identity.Name(owner.Value, EntityManager))),
            ent.Owner,
            ent.Owner,
            PopupType.Medium);
    }

    private void UnlockAutoDispenser(Entity<SyndicatePaiComponent> ent)
    {
        if (ent.Comp.AutoDispenserUnlocked)
            return;

        ent.Comp.AutoDispenserUnlocked = true;
        ent.Comp.AutoDispenserEnabled = false;
        ent.Comp.AutoHealthThreshold = 40f;

        GrantInnateTool(ent, ent.Comp.AutoHypoPrototype, entityTarget: true, grantAction: false);

        if (TryGetAutoHypo(ent, out var autoHypo) && autoHypo != null)
        {
            ClearHypoReservoir(autoHypo.Value);
            ConfigureEmergencyFullDump(autoHypo.Value);
        }

        // Доступ к мед. окну для настройки, если медицинского модуля ещё нет
        if (ent.Comp.OpenMedicalActionEntity == null || TerminatingOrDeleted(ent.Comp.OpenMedicalActionEntity.Value))
            _serverActions.AddAction(ent.Owner, ref ent.Comp.OpenMedicalActionEntity, ent.Comp.OpenMedicalAction);

        Dirty(ent);
        _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-module-auto-unlocked"), ent.Owner, ent.Owner);
    }

    private void OnSelectMessage(Entity<SyndicatePaiComponent> ent, ref SyndicatePaiSelectReagentMessage args)
    {
        TrySelectReagent(ent, args.Actor, args.Index, args.AutoReservoir);
    }

    private void OnSetAutoEnabled(Entity<SyndicatePaiComponent> ent, ref SyndicatePaiSetAutoEnabledMessage args)
    {
        if (!ent.Comp.AutoDispenserUnlocked)
        {
            _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-module-locked"), ent.Owner, args.Actor);
            return;
        }

        ent.Comp.AutoDispenserEnabled = args.Enabled;
        Dirty(ent);
        UpdateUiState(ent);
    }

    private void OnSetAutoThreshold(Entity<SyndicatePaiComponent> ent, ref SyndicatePaiSetAutoThresholdMessage args)
    {
        if (!ent.Comp.AutoDispenserUnlocked)
        {
            _serverPopup.PopupEntity(Loc.GetString("syndicate-pai-module-locked"), ent.Owner, args.Actor);
            return;
        }

        ent.Comp.AutoHealthThreshold = Math.Clamp(args.Threshold, 5f, 90f);
        Dirty(ent);
        UpdateUiState(ent);
    }
}
