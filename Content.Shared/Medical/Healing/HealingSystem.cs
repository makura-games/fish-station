using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared._Fish.Skills; // Fish-edit: РЅР°РІС‹РєРё РјРµРґРёС†РёРЅС‹
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Shared.Medical.Healing;

public sealed partial class HealingSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private MobThresholdSystem _mobThresholdSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedSkillSystem _skills = default!; // Fish-edit: РјРЅРѕР¶РёС‚РµР»СЊ РІСЂРµРјРµРЅРё Р»РµС‡РµРЅРёСЏ

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HealingComponent, UseInHandEvent>(OnHealingUse);
        SubscribeLocalEvent<HealingComponent, AfterInteractEvent>(OnHealingAfterInteract);
        SubscribeLocalEvent<DamageableComponent, HealingDoAfterEvent>(OnDoAfter);
    }

    private void OnDoAfter(Entity<DamageableComponent> target, ref HealingDoAfterEvent args)
    {

        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp(args.Used, out HealingComponent? healing))
            return;

        if (!TryComp<InjurableComponent>(target, out var injurable))
            return;

        if (healing.DamageContainers is not null &&
            injurable.DamageContainer is not null &&
            !healing.DamageContainers.Contains(injurable.DamageContainer.Value))
        {
            return;
        }

        TryComp<BloodstreamComponent>(target, out var bloodstream);

        // Fish-start: РІРµР»РёС‡РёРЅР° Р»РµС‡РµРЅРёСЏ РѕС‚ РЅР°РІС‹РєР° РјРµРґРёС†РёРЅС‹
        var healAmountMod = _skills.GetSkillQualityMultiplier(args.User, "FishSkillMedical");
        // Fish-end

        // Heal some bloodloss damage.
        if (healing.BloodlossModifier != 0 && bloodstream != null)
        {
            var isBleeding = bloodstream.BleedAmount > 0;
            // Fish-edit: РјРѕРґРёС„РёРєР°С‚РѕСЂ РєСЂРѕРІРѕС‚РµС‡РµРЅРёСЏ РјР°СЃС€С‚Р°Р±РёСЂСѓРµС‚СЃСЏ РЅР°РІС‹РєРѕРј
            _bloodstreamSystem.TryModifyBleedAmount((target.Owner, bloodstream), healing.BloodlossModifier * healAmountMod);
            if (isBleeding != bloodstream.BleedAmount > 0)
            {
                var popup = (args.User == target.Owner)
                    ? Loc.GetString("medical-item-stop-bleeding-self")
                    : Loc.GetString("medical-item-stop-bleeding", ("target", Identity.Entity(target.Owner, EntityManager)));
                _popupSystem.PopupEntity(popup, target, args.User);
            }
        }

        // Restores missing blood
        if (healing.ModifyBloodLevel != 0 && bloodstream != null)
            // Fish-edit: РѕР±СЉС‘Рј РєСЂРѕРІРё РѕС‚ РЅР°РІС‹РєР°
            _bloodstreamSystem.TryModifyBloodLevel((target.Owner, bloodstream), healing.ModifyBloodLevel * healAmountMod);

        // Fish-edit: СѓСЂРѕРЅ Р»РµС‡РµРЅРёСЏ * HEAL_AMOUNT_MOD
        if (!_damageable.TryChangeDamage(target.Owner, healing.Damage * _damageable.UniversalTopicalsHealModifier * healAmountMod, out var healed, true, origin: args.Args.User) && healing.BloodlossModifier != 0)
            return;

        var total = healed.GetTotal();

        // Re-verify that we can heal the damage.
        var dontRepeat = false;
        if (TryComp<StackComponent>(args.Used.Value, out var stackComp))
        {
            _stacks.ReduceCount((args.Used.Value, stackComp), 1);

            if (_stacks.GetCount((args.Used.Value, stackComp)) <= 0)
                dontRepeat = true;
        }
        // Starlight start
        else if (healing.SolutionDrain)
        {
            Entity<SolutionComponent>? solutionEntity = null;
            if (_solutionContainerSystem.ResolveSolution(args.Used.Value, "injector", ref solutionEntity, out var solution))
            {
                var reagentsToRemove = new List<(ReagentQuantity Reagent, FixedPoint2 Amount)>();
                foreach(var reagent in solution.Contents)
                {
                    var drainReagent = healing.ReagentsToDrain.FirstOrNull(drain =>
                        drain.Reagent == reagent.Reagent && reagent.Quantity >= drain.Quantity);
                    if (solutionEntity != null && drainReagent is { } drain)
                        reagentsToRemove.Add((reagent, drain.Quantity));
                }

                foreach (var (reagent, amount) in reagentsToRemove)
                {
                    if (solutionEntity != null)
                        _solutionContainerSystem.RemoveReagent(solutionEntity.Value, reagent.Reagent, amount);
                }

                if (!solution.Contents.Any(sol => healing.ReagentsToDrain.Any(req => req.Reagent == sol.Reagent && sol.Quantity >= req.Quantity)))
                    dontRepeat = true;
            }
        }
        // Starlight end
        else
        {
            PredictedQueueDel(args.Used.Value);
        }

        if (target.Owner != args.User)
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(args.User):user} healed {ToPrettyString(target.Owner):target} for {total:damage} damage");
        }
        else
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(args.User):user} healed themselves for {total:damage} damage");
        }

        _audio.PlayPredicted(healing.HealingEndSound, target.Owner, args.User);

        // Logic to determine the whether or not to repeat the healing action
        args.Repeat = HasDamage((args.Used.Value, healing), target) && !dontRepeat;
        args.Handled = true;

        if (!args.Repeat)
        {
            _popupSystem.PopupEntity(Loc.GetString("medical-item-finished-using", ("item", args.Used)), target.Owner, args.User);
            return;
        }

        // Update our self heal delay so it shortens as we heal more damage.
        if (args.User == target.Owner)
        {
            // Fish-start: РїРѕРІС‚РѕСЂ self-heal СѓС‡РёС‚С‹РІР°РµС‚ РЅР°РІС‹Рє РјРµРґРёС†РёРЅС‹
            args.Args.Delay = healing.Delay
                * GetScaledHealingPenalty(target.Owner, healing.SelfHealPenaltyMultiplier)
                * _skills.GetSkillDelayMultiplier(args.User, "FishSkillMedical");
            // Fish-end
        }
    }

    private bool HasDamage(Entity<HealingComponent> healing, Entity<DamageableComponent> target)
    {
        var damageableDict = _damageable.GetAllDamage(target.AsNullable()).DamageDict;
        var healingDict = healing.Comp.Damage.DamageDict;
        foreach (var type in healingDict)
        {
            if (damageableDict.TryGetValue(type.Key, out var amount)
                && amount > 0
                && (type.Key != "Mangleness" || type.Value < 0)) // Sunrise-Edit вЂ” Mangleness РЅРµР»СЊР·СЏ РЅР°РЅРѕСЃРёС‚СЊ Р»РµС‡РµР±РЅС‹Рј РїСЂРµРґРјРµС‚РѕРј
            {
                return true;
            }
        }

        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            // Is ent missing blood that we can restore?
            if (healing.Comp.ModifyBloodLevel > 0
                && _solutionContainerSystem.ResolveSolution(target.Owner, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution)
                && _bloodstreamSystem.GetBloodLevel((target, bloodstream)) < 1)
            {
                return true;
            }

            // Is ent bleeding and can we stop it?
            if (healing.Comp.BloodlossModifier < 0 && bloodstream.BleedAmount > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void OnHealingUse(Entity<HealingComponent> healing, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryHeal(healing, args.User, args.User))
            args.Handled = true;
    }

    private void OnHealingAfterInteract(Entity<HealingComponent> healing, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        if (TryHeal(healing, args.Target.Value, args.User))
            args.Handled = true;
    }

    private bool TryHeal(Entity<HealingComponent> healing, Entity<DamageableComponent?> target, EntityUid user)
    {
        if (!Resolve(target, ref target.Comp, false))
            return false;

        if (!TryComp<InjurableComponent>(target, out var injurable))
            return false;

        if (healing.Comp.DamageContainers is not null &&
            injurable.DamageContainer is not null &&
            !healing.Comp.DamageContainers.Contains(injurable.DamageContainer.Value))
        {
            return false;
        }

        if (user != target.Owner && !_interactionSystem.InRangeUnobstructed(user, target.Owner, popup: true))
            return false;

        if (TryComp<StackComponent>(healing, out var stack) && stack.Count < 1)
            return false;

        //Sunrise-Start
        if (TryComp<MobStateComponent>(target.Owner, out var state))
        {
            if (!healing.Comp.WorksOnTheDead && _mobState.IsDead(target.Owner, state))
                return false;
        }
        //Sunrise-End

        // Starlight start
        if (healing.Comp.SolutionDrain)
        {
            Entity<SolutionComponent>? solutionEntity = null;
            if (_solutionContainerSystem.ResolveSolution(healing.Owner, "injector", ref solutionEntity, out var solution))
            {
                if (!solution.Contents.Any(sol => healing.Comp.ReagentsToDrain.Any(req => req.Reagent == sol.Reagent && sol.Quantity >= req.Quantity)))
                {
                    _popupSystem.PopupEntity(Loc.GetString("medical-item-solution-missing", ("item", healing.Owner)), healing.Owner, user);
                    return false;
                }
            }
            else
                return false;
        }
        // Starlight end
        if (!HasDamage(healing, target!))
        {
            _popupSystem.PopupEntity(Loc.GetString("medical-item-cant-use", ("item", healing.Owner)), healing, user);
            return false;
        }

        _audio.PlayPredicted(healing.Comp.HealingBeginSound, healing, user);

        var isNotSelf = user != target.Owner;

        if (isNotSelf)
        {
            var msg = Loc.GetString("medical-item-popup-target", ("user", Identity.Entity(user, EntityManager)), ("item", healing.Owner));
            _popupSystem.PopupEntity(msg, target, target, PopupType.Medium);
        }

        var delay = isNotSelf
            ? healing.Comp.Delay
            : healing.Comp.Delay * GetScaledHealingPenalty(target, healing.Comp.SelfHealPenaltyMultiplier);

        // Fish-start: РјРЅРѕР¶РёС‚РµР»СЊ РІСЂРµРјРµРЅРё Р»РµС‡РµРЅРёСЏ РѕС‚ РЅР°РІС‹РєР° РјРµРґРёС†РёРЅС‹
        delay *= _skills.GetSkillDelayMultiplier(user, "FishSkillMedical");
        // Fish-end

        var doAfterEventArgs =
            new DoAfterArgs(EntityManager, user, delay, new HealingDoAfterEvent(), target, target: target, used: healing)
            {
                // Didn't break on damage as they may be trying to prevent it and
                // not being able to heal your own ticking damage would be frustrating.
                NeedHand = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
            };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
        return true;
    }

    /// <summary>
    /// Scales the self-heal penalty based on the amount of damage taken
    /// </summary>
    /// <param name="ent">Entity we're healing</param>
    /// <param name="mod">Maximum modifier we can have.</param>
    /// <returns>Modifier we multiply our healing time by</returns>
    public float GetScaledHealingPenalty(Entity<DamageableComponent?, MobThresholdsComponent?> ent, float mod)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2, false))
            return mod;

        if (!_mobThresholdSystem.TryGetThresholdForState(ent, MobState.Critical, out var amount, ent.Comp2))
            return 1;

        var percentDamage = (float)(_damageable.GetTotalDamage(ent) / amount);
        //basically make it scale from 1 to the multiplier.

        var output = percentDamage * (mod - 1) + 1;
        return Math.Max(output, 1);
    }

    //Sunrise-Start
    public float GetScaledHealingPenaltyFixed(Entity<HealingComponent> healing, EntityUid user) //Sunrise-Edit
    {
        if (!TryComp<MobThresholdsComponent>(user, out var mobThreshold) ||
            !TryComp<DamageableComponent>(user, out var damageable))
            return 1;
        if (!_mobThresholdSystem.TryGetThresholdForState(user, MobState.Critical, out var amount, mobThreshold))
            return 1;

        var percentDamage = (float)(_damageable.GetTotalDamage((user, damageable)) / amount);
        //basically make it scale from 1 to the multiplier.
        var modifier = percentDamage * (healing.Comp.SelfHealPenaltyMultiplier - 1) + 1;
        return Math.Max(modifier, 1.5f);
    }
    //Sunrise-End
}
