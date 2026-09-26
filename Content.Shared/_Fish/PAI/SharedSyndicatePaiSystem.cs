using System.Diagnostics.CodeAnalysis;
using Content.Shared._Sunrise.SolutionRegenerationSwitcher;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Fish.PAI;

/// <summary>
/// Shared API for Syndicate pAI medical suite and owner helpers.
/// </summary>
public abstract partial class SharedSyndicatePaiSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextUiRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SyndicatePaiComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SyndicatePaiComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SyndicatePaiComponent, SyndicatePaiOpenMedicalEvent>(OnOpenMedical);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Периодически обновляем объём реагентов в открытом мед. UI
        if (_timing.CurTime < _nextUiRefresh)
            return;

        _nextUiRefresh = _timing.CurTime + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<SyndicatePaiComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.MedicalUnlocked && !comp.AutoDispenserUnlocked)
                continue;

            if (!_ui.IsUiOpen(uid, SyndicatePaiUiKey.Key))
                continue;

            UpdateUiState((uid, comp));
        }
    }

    private void OnMapInit(Entity<SyndicatePaiComponent> ent, ref MapInitEvent args)
    {
        // Действия модулей выдаются только после покупки в магазине
        Dirty(ent);
    }

    private void OnShutdown(Entity<SyndicatePaiComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.OpenMedicalActionEntity);
        _actions.RemoveAction(ent.Owner, ent.Comp.ScanOwnerActionEntity);
        _actions.RemoveAction(ent.Owner, ent.Comp.DoorHackActionEntity);
        _actions.RemoveAction(ent.Owner, ent.Comp.SecRecordsActionEntity);
    }

    private void OnOpenMedical(Entity<SyndicatePaiComponent> ent, ref SyndicatePaiOpenMedicalEvent args)
    {
        if (args.Handled)
            return;

        if (!ent.Comp.MedicalUnlocked && !ent.Comp.AutoDispenserUnlocked)
        {
            _popup.PopupClient(Loc.GetString("syndicate-pai-module-locked"), ent.Owner, args.Performer);
            args.Handled = true;
            return;
        }

        _ui.TryToggleUi(ent.Owner, SyndicatePaiUiKey.Key, args.Performer);
        args.Handled = true;
        UpdateUiState(ent);
    }

    /// <summary>
    /// Inject current hypo contents into the bound master (owner only).
    /// </summary>
    public bool TryInjectOwner(Entity<SyndicatePaiComponent> ent, EntityUid user, bool quiet = false)
    {
        if (!CanInjectOwner(ent, user, out var target, out var hypo, quiet))
            return false;

        var hypoUid = hypo!.Value;
        var targetUid = target!.Value;

        _interaction.InteractUsing(
            user,
            hypoUid,
            targetUid,
            Transform(targetUid).Coordinates,
            checkCanInteract: false,
            checkCanUse: false,
            needHand: false);

        UpdateUiState(ent);
        return true;
    }

    public bool CanInjectOwner(
        Entity<SyndicatePaiComponent> ent,
        EntityUid user,
        out EntityUid? target,
        out EntityUid? hypo,
        bool quiet = false)
    {
        target = null;
        hypo = null;

        if (!ent.Comp.MedicalUnlocked)
        {
            if (!quiet)
                _popup.PopupClient(Loc.GetString("syndicate-pai-module-locked"), ent.Owner, user);
            return false;
        }

        if (!TryGetManualHypo(ent, out hypo) || hypo == null)
        {
            if (!quiet)
                _popup.PopupClient(Loc.GetString("syndicate-pai-no-hypo"), ent.Owner, user);
            return false;
        }

        if (!TryGetOwnerTarget(ent, out target) || target == null)
        {
            if (!quiet)
                _popup.PopupClient(Loc.GetString("syndicate-pai-no-owner"), ent.Owner, user);
            return false;
        }

        if (!IsHeldByOwner(ent, target.Value))
        {
            if (!quiet)
                _popup.PopupClient(Loc.GetString("syndicate-pai-not-in-owner-inventory"), ent.Owner, user);
            return false;
        }

        if (!HasComp<BloodstreamComponent>(target.Value))
        {
            if (!quiet)
                _popup.PopupClient(Loc.GetString("syndicate-pai-target-not-organic"), ent.Owner, user);
            return false;
        }

        return true;
    }

    public bool TrySelectReagent(Entity<SyndicatePaiComponent> ent, EntityUid user, int index, bool autoReservoir = false, bool quiet = false)
    {
        if (autoReservoir)
        {
            if (!ent.Comp.AutoDispenserUnlocked)
            {
                if (!quiet)
                    _popup.PopupClient(Loc.GetString("syndicate-pai-module-locked"), ent.Owner, user);
                return false;
            }
        }
        else if (!ent.Comp.MedicalUnlocked)
        {
            if (!quiet)
                _popup.PopupClient(Loc.GetString("syndicate-pai-module-locked"), ent.Owner, user);
            return false;
        }

        if (autoReservoir)
        {
            if (!TryGetAutoHypo(ent, out var autoHypo) || autoHypo == null)
            {
                if (!quiet)
                    _popup.PopupClient(Loc.GetString("syndicate-pai-no-auto-hypo"), ent.Owner, user);
                return false;
            }

            return TrySelectReagentOnHypo(ent, user, autoHypo.Value, index, quiet);
        }

        if (!TryGetManualHypo(ent, out var hypo) || hypo == null)
        {
            if (!quiet)
                _popup.PopupClient(Loc.GetString("syndicate-pai-no-hypo"), ent.Owner, user);
            return false;
        }

        return TrySelectReagentOnHypo(ent, user, hypo.Value, index, quiet);
    }

    /// <summary>
    /// Выбор объёма одной инъекции для ручного гипо.
    /// </summary>
    public bool TrySetTransferAmount(Entity<SyndicatePaiComponent> ent, EntityUid user, FixedPoint2 amount, bool quiet = false)
    {
        if (!ent.Comp.MedicalUnlocked)
        {
            if (!quiet)
                _popup.PopupClient(Loc.GetString("syndicate-pai-module-locked"), ent.Owner, user);
            return false;
        }

        if (!TryGetManualHypo(ent, out var hypo) || hypo == null)
        {
            if (!quiet)
                _popup.PopupClient(Loc.GetString("syndicate-pai-no-hypo"), ent.Owner, user);
            return false;
        }

        if (!TryComp<InjectorComponent>(hypo.Value, out var injector))
            return false;

        if (!_prototypes.Resolve(injector.ActiveModeProtoId, out InjectorModePrototype? mode) ||
            !mode.TransferAmounts.Contains(amount))
        {
            if (!quiet)
                _popup.PopupClient(Loc.GetString("syndicate-pai-invalid-dose"), ent.Owner, user);
            return false;
        }

        injector.CurrentTransferAmount = amount;
        Dirty(hypo.Value, injector);
        UpdateUiState(ent);
        return true;
    }

    private bool TrySelectReagentOnHypo(Entity<SyndicatePaiComponent> ent, EntityUid user, EntityUid hypo, int index, bool quiet)
    {
        if (!TryComp<SolutionRegenerationSwitcherComponent>(hypo, out var switcher))
        {
            if (!quiet)
                _popup.PopupClient(Loc.GetString("syndicate-pai-no-reagents"), ent.Owner, user);
            return false;
        }

        if (index < 0 || index >= switcher.Options.Count)
            return false;

        if (!TryComp<SolutionRegenerationComponent>(hypo, out var regeneration))
            return false;

        var reagent = switcher.Options[index];
        if (regeneration.Generated.ContainsReagent(reagent.Reagent))
        {
            if (!quiet)
                _popup.PopupClient(Loc.GetString("solution-regeneration-switcher-already-select"), ent.Owner, user);
            return false;
        }

        if (!switcher.KeepSolution &&
            TryGetHypoSolution(hypo, out var solutionEnt, out _))
        {
            _solutions.RemoveAllSolution(solutionEnt.Value);
        }

        regeneration.ChangeGenerated(reagent);
        switcher.CurrentIndex = index;
        Dirty(hypo, switcher);

        if (_prototypes.TryIndex(reagent.Reagent.Prototype, out ReagentPrototype? proto) && !quiet)
        {
            _popup.PopupClient(
                Loc.GetString("solution-regeneration-switcher-switched", ("reagent", proto.LocalizedName)),
                ent.Owner,
                user);
        }

        UpdateUiState(ent);
        return true;
    }

    /// <summary>
    /// Ручной медицинский гипо (без маркера автодозатора).
    /// </summary>
    public bool TryGetManualHypo(Entity<SyndicatePaiComponent> ent, out EntityUid? hypo)
    {
        hypo = null;

        if (!_container.TryGetContainer(ent.Owner, SyndicatePaiComponent.InnateItemContainerId, out var container))
            return false;

        foreach (var contained in container.ContainedEntities)
        {
            if (HasComp<SyndicatePaiAutoHypoComponent>(contained))
                continue;

            if (!HasComp<InjectorComponent>(contained))
                continue;

            if (HasComp<SolutionRegenerationSwitcherComponent>(contained) ||
                HasComp<SolutionRegenerationComponent>(contained))
            {
                hypo = contained;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Экстренный гипо автодозатора.
    /// </summary>
    public bool TryGetAutoHypo(Entity<SyndicatePaiComponent> ent, out EntityUid? hypo)
    {
        hypo = null;

        if (!_container.TryGetContainer(ent.Owner, SyndicatePaiComponent.InnateItemContainerId, out var container))
            return false;

        foreach (var contained in container.ContainedEntities)
        {
            if (!HasComp<SyndicatePaiAutoHypoComponent>(contained))
                continue;

            hypo = contained;
            return true;
        }

        return false;
    }

    public bool TryGetHypo(Entity<SyndicatePaiComponent> ent, out EntityUid? hypo)
    {
        return TryGetManualHypo(ent, out hypo);
    }

    public bool TryGetAnalyzer(Entity<SyndicatePaiComponent> ent, out EntityUid? analyzer)
    {
        analyzer = null;

        if (!_container.TryGetContainer(ent.Owner, SyndicatePaiComponent.InnateItemContainerId, out var container))
            return false;

        foreach (var contained in container.ContainedEntities)
        {
            // HealthAnalyzerComponent на сервере; в Shared ищем по UI ключу анализатора
            if (!_ui.HasUi(contained, Content.Shared.MedicalScanner.HealthAnalyzerUiKey.Key))
                continue;

            analyzer = contained;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Injection/scan target is always the bound master.
    /// </summary>
    public bool TryGetOwnerTarget(Entity<SyndicatePaiComponent> ent, out EntityUid? target)
    {
        target = null;

        if (ent.Comp.Master is not { Valid: true } master || TerminatingOrDeleted(master))
            return false;

        if (!HasComp<BloodstreamComponent>(master))
            return false;

        target = master;
        return true;
    }

    /// <summary>
    /// True when the pAI is inside the owner's inventory, hands, storage or PDA.
    /// </summary>
    public bool IsHeldByOwner(Entity<SyndicatePaiComponent> ent, EntityUid owner)
    {
        if (!TryGetCarrier(ent.Owner, out var carrier) || carrier == null)
            return false;

        return carrier == owner;
    }

    public bool TryGetCarrier(EntityUid pai, out EntityUid? carrier)
    {
        carrier = null;
        var current = Transform(pai).ParentUid;

        while (current.IsValid() && !TerminatingOrDeleted(current))
        {
            if (HasComp<HandsComponent>(current) ||
                HasComp<InventoryComponent>(current) ||
                HasComp<StorageComponent>(current))
            {
                if (HasComp<MobStateComponent>(current) || HasComp<BloodstreamComponent>(current))
                {
                    carrier = current;
                    return true;
                }
            }

            var parent = Transform(current).ParentUid;
            if (parent == current)
                break;
            current = parent;
        }

        return false;
    }

    public void TryImprintMaster(Entity<SyndicatePaiComponent> ent, EntityUid master, EntityUid user)
    {
        if (!HasComp<BloodstreamComponent>(master))
        {
            _popup.PopupClient(Loc.GetString("syndicate-pai-imprint-failed"), ent.Owner, user);
            return;
        }

        ent.Comp.Master = master;
        Dirty(ent);
        _popup.PopupClient(
            Loc.GetString("syndicate-pai-imprint-success", ("master", Identity.Name(master, EntityManager))),
            ent.Owner,
            user);
        UpdateUiState(ent);
    }

    protected void ClearHypoReservoir(EntityUid hypo)
    {
        if (TryGetHypoSolution(hypo, out var solutionEnt, out _))
            _solutions.RemoveAllSolution(solutionEnt.Value);
    }

    /// <summary>
    /// Экстренный гипо вводит весь объём резервуара за раз.
    /// </summary>
    protected void ConfigureEmergencyFullDump(EntityUid hypo)
    {
        if (!TryComp<InjectorComponent>(hypo, out var injector))
            return;

        if (injector.CurrentTransferAmount == null)
            return;

        injector.CurrentTransferAmount = null;
        Dirty(hypo, injector);
    }

    protected void UpdateUiState(Entity<SyndicatePaiComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, SyndicatePaiUiKey.Key))
            return;

        var state = BuildUiState(ent);
        _ui.SetUiState(ent.Owner, SyndicatePaiUiKey.Key, state);
    }

    protected SyndicatePaiBoundUserInterfaceState BuildUiState(Entity<SyndicatePaiComponent> ent)
    {
        var state = new SyndicatePaiBoundUserInterfaceState
        {
            CurrentReagentIndex = 0,
            MedicalUnlocked = ent.Comp.MedicalUnlocked,
            AutoDispenserUnlocked = ent.Comp.AutoDispenserUnlocked,
            AutoDispenserEnabled = ent.Comp.AutoDispenserEnabled,
            AutoHealthThreshold = ent.Comp.AutoHealthThreshold,
            AutoReagentIndex = 0,
        };

        if (TryGetCarrier(ent.Owner, out var carrier) && carrier != null)
            state.CarrierName = Identity.Name(carrier.Value, EntityManager);

        if (ent.Comp.Master is { Valid: true } master && !TerminatingOrDeleted(master))
            state.MasterName = Identity.Name(master, EntityManager);

        state.CanInjectOwner = CanInjectOwner(ent, ent.Owner, out _, out _, quiet: true);

        var cooldown = ent.Comp.NextAutoInjectTime - _timing.CurTime;
        state.AutoCooldownRemaining = cooldown > TimeSpan.Zero ? (float)cooldown.TotalSeconds : 0f;

        FillHypoUiState(ent, TryGetManualHypo, ref state.CurrentReagent, ref state.CurrentVolume, ref state.MaxVolume,
            state.Reagents, ref state.CurrentReagentIndex);

        if (TryGetManualHypo(ent, out var manualHypo) && manualHypo != null &&
            TryComp<InjectorComponent>(manualHypo.Value, out var injector))
        {
            state.InjectTransferAmount = injector.CurrentTransferAmount?.Float() ?? 5f;
            if (_prototypes.Resolve(injector.ActiveModeProtoId, out InjectorModePrototype? mode))
            {
                foreach (var amount in mode.TransferAmounts)
                    state.InjectTransferAmounts.Add(amount.Float());
            }
        }

        FillHypoUiState(ent, TryGetAutoHypo, ref state.AutoReagent, ref state.AutoVolume, ref state.AutoMaxVolume,
            state.AutoReagents, ref state.AutoReagentIndex);

        return state;
    }

    private delegate bool HypoGetter(Entity<SyndicatePaiComponent> ent, out EntityUid? hypo);

    private void FillHypoUiState(
        Entity<SyndicatePaiComponent> ent,
        HypoGetter getter,
        ref string? reagentName,
        ref float volume,
        ref float maxVolume,
        List<SyndicatePaiReagentEntry> reagents,
        ref int reagentIndex)
    {
        if (!getter(ent, out var hypo) || hypo == null)
            return;

        var hasSwitcher = TryComp<SolutionRegenerationSwitcherComponent>(hypo.Value, out var switcher);
        if (hasSwitcher && switcher != null)
        {
            reagentIndex = switcher.CurrentIndex;
            for (var i = 0; i < switcher.Options.Count; i++)
            {
                var option = switcher.Options[i];
                var name = option.Reagent.Prototype;
                if (_prototypes.TryIndex(option.Reagent.Prototype, out ReagentPrototype? proto))
                    name = proto.LocalizedName;

                reagents.Add(new SyndicatePaiReagentEntry
                {
                    Id = option.Reagent.Prototype,
                    Name = name,
                    Index = i,
                });
            }
        }

        if (!TryComp<SolutionRegenerationComponent>(hypo.Value, out var regen) ||
            !TryGetHypoSolution(hypo.Value, out _, out var solution))
            return;

        volume = solution.Volume.Float();
        maxVolume = solution.MaxVolume.Float();

        if (!hasSwitcher && regen.Generated.Contents.Count > 0)
        {
            // Смесь без переключателя — показываем состав генерации
            var parts = new List<string>();
            foreach (var quantity in regen.Generated.Contents)
            {
                if (_prototypes.TryIndex(quantity.Reagent.Prototype, out ReagentPrototype? mixProto))
                    parts.Add(mixProto.LocalizedName);
                else
                    parts.Add(quantity.Reagent.Prototype);
            }

            reagentName = string.Join(", ", parts);
            return;
        }

        if (solution.Contents.Count <= 0)
            return;

        var primary = solution.GetPrimaryReagentId();
        if (primary != null && _prototypes.TryIndex(primary.Value.Prototype, out ReagentPrototype? current) && current != null)
            reagentName = current.LocalizedName;
        else if (primary != null)
            reagentName = primary.Value.Prototype;
    }

    protected bool TryGetHypoSolution(
        EntityUid hypo,
        [NotNullWhen(true)] out Entity<SolutionComponent>? solutionEnt,
        [NotNullWhen(true)] out Solution? solution)
    {
        solutionEnt = null;
        solution = null;

        if (TryComp<SolutionComponent>(hypo, out var solutionComp))
        {
            solutionEnt = (hypo, solutionComp);
            solution = solutionComp.Solution;
            return true;
        }

        if (TryComp<InjectorComponent>(hypo, out var injector) &&
            _solutions.TryGetSolution(hypo, injector.SolutionName, out solutionEnt, out solution))
        {
            return true;
        }

        return _solutions.TryGetSolution(hypo, "hypospray", out solutionEnt, out solution);
    }
}
