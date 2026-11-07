using Content.Server.NPC.HTN;
using Content.Shared._Fish.Xenobiology;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Temperature.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Fish.Xenobiology;

/// <summary>Runs feeding, maturation, inherited traits and controlled division of laboratory slimes.</summary>
public sealed partial class FishXenobiologySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private SatiationSystem _satiation = default!;

    [Dependency] private EntityQuery<FishXenobiologySlimeComponent> _slimeQuery = default!;
    [Dependency] private EntityQuery<HTNComponent> _htnQuery = default!;
    [Dependency] private EntityQuery<SatiationComponent> _satiationQuery = default!;
    [Dependency] private EntityQuery<TemperatureComponent> _temperatureQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _transformQuery = default!;

    /// <summary>Map-wide colony limit shared by division and culture hatching; unprocessed corpses also count.</summary>
    public const int PopulationLimit = 64;

    private const float UpdateInterval = 1f;
    private const float MinimumGrowthHunger = 25f;
    private const float DivisionHunger = 50f;
    private const string CalmFaction = "FishXenoSlimeCalm";
    private const string HungryFaction = "FishXenoSlimeHungry";
    private static readonly ProtoId<FishXenobiologyBreedPrototype> Rainbow = "FishRainbow";
    private static readonly ProtoId<DamageTypePrototype> HeatDamage = "Heat";
    private static readonly ProtoId<DamageTypePrototype> ColdDamage = "Cold";
    private static readonly ProtoId<DamageTypePrototype> StarvationDamage = "Poison";

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishXenobiologySlimeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FishXenobiologySlimeComponent, IngestingEvent>(OnIngesting);
        SubscribeLocalEvent<FishXenobiologySlimeComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FishXenobiologySlimeComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<FishXenobiologySlimeComponent, EntGotRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<FishXenobiologySlimeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<FishXenobiologySlimeComponent, FishXenobiologyDivideDoAfterEvent>(OnDivideDoAfter);
        SubscribeLocalEvent<FishXenobiologySlimeComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<FishXenobiologySlimeComponent> ent, ref MapInitEvent args)
    {
        RefreshAppearance(ent);
        SetSuspended(ent, _container.IsEntityInContainer(ent));
    }

    private void OnIngesting(Entity<FishXenobiologySlimeComponent> ent, ref IngestingEvent args)
    {
        if (ent.Comp.Consumed || !_mobState.IsAlive(ent))
            return;

        var protein = 0f;
        var nutriment = 0f;
        foreach (var (reagent, quantity) in args.Split.Contents)
        {
            switch (reagent.Prototype.Id)
            {
                case "Protein":
                case "UncookedAnimalProteins":
                    protein += quantity.Float();
                    break;
                case "Nutriment":
                    nutriment += quantity.Float();
                    break;
            }
        }

        ent.Comp.ProteinIntake += protein;
        ent.Comp.NutrimentIntake += nutriment;
        ent.Comp.Biomass = Math.Min(120f, ent.Comp.Biomass + protein + nutriment * 0.75f);

        // Fish-edit: успокаиваем по факту проглоченной пищи, не ожидая её переработки желудком.
        if (protein + nutriment > 0)
        {
            ent.Comp.FeedingGrace = 20f;
            SetHostile(ent, false);
        }
    }

    private void OnMobStateChanged(Entity<FishXenobiologySlimeComponent> ent, ref MobStateChangedEvent args)
    {
        RefreshAppearance(ent);
    }

    private void OnInserted(Entity<FishXenobiologySlimeComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        SetSuspended(ent, true);
    }

    private void OnRemoved(Entity<FishXenobiologySlimeComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        SetSuspended(ent, false);
    }

    private void OnGetVerbs(Entity<FishXenobiologySlimeComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !ent.Comp.Adult || !_mobState.IsAlive(ent))
            return;

        var uid = ent.Owner;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fish-xeno-divide"),
            Disabled = !CanDivide(uid, user),
            Message = Loc.GetString("fish-xeno-divide-requirements"),
            Act = () => TryStartDivision(uid, user),
        });
    }

    private void OnDivideDoAfter(Entity<FishXenobiologySlimeComponent> ent, ref FishXenobiologyDivideDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = TryDivide(ent, args.User);
    }

    private void OnExamined(Entity<FishXenobiologySlimeComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var breed = _prototype.Index(ent.Comp.Breed);
        args.PushMarkup(Loc.GetString("fish-xeno-examine",
            ("breed", Loc.GetString(breed.Name)),
            ("generation", ent.Comp.Generation),
            ("stage", Loc.GetString(ent.Comp.Adult ? "fish-xeno-stage-adult" : "fish-xeno-stage-baby")),
            ("biomass", MathF.Round(ent.Comp.Biomass))));
        if (ent.Comp.Hostile)
            args.PushMarkup(Loc.GetString("fish-xeno-examine-hungry"));
    }

    /// <summary>Starts a cancellable division interaction for a conscious handler in reach.</summary>
    public bool TryStartDivision(EntityUid slime, EntityUid user)
    {
        if (!CanDivide(slime, user))
            return false;

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(3),
            new FishXenobiologyDivideDoAfterEvent(), slime, target: slime)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1.5f,
        });
    }

    /// <summary>Checks nutrition, adulthood, containment, player control and the colony population limit.</summary>
    public bool CanDivide(EntityUid slime, EntityUid? user = null)
    {
        // Fish-start: индукция закрепляет взрослого слайма за промышленным циклом.
        if (HasComp<FishXenobiologyIndustrialInductionComponent>(slime))
            return false;
        // Fish-end

        if (TerminatingOrDeleted(slime) || !_slimeQuery.TryComp(slime, out var state) ||
            !state.Adult || state.Consumed || state.Suspended || !_mobState.IsAlive(slime) ||
            _container.IsEntityInContainer(slime) || HasPlayerMind(slime) ||
            !_satiationQuery.TryComp(slime, out var satiation) ||
            (_satiation.GetValueOrNull((slime, satiation), SatiationSystem.Hunger) ?? 0) < DivisionHunger)
            return false;

        var breed = _prototype.Index(state.Breed);
        if (state.Biomass < breed.DivisionBiomass || !IsComfortable(slime, state, breed))
            return false;

        if (user is { } handler &&
            (TerminatingOrDeleted(handler) || !_actionBlocker.CanInteract(handler, slime) ||
             !_interaction.InRangeUnobstructed(handler, slime)))
            return false;

        var transform = _transformQuery.GetComponent(slime);
        var count = 0;
        var query = EntityQueryEnumerator<FishXenobiologySlimeComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var otherTransform))
        {
            if (otherTransform.MapID == transform.MapID)
                count++;
        }

        return count - 1 + Math.Clamp(state.Offspring, 2, 4) <= PopulationLimit;
    }

    /// <summary>Consumes one adult and creates a bounded litter carrying independently sampled inherited traits.</summary>
    public bool TryDivide(EntityUid slime, EntityUid? user = null)
    {
        if (!CanDivide(slime, user) || !_slimeQuery.TryComp(slime, out var parent))
            return false;

        var breed = _prototype.Index(parent.Breed);
        var coordinates = Transform(slime).Coordinates;
        var offspring = Math.Clamp(parent.Offspring, 2, 4);

        // Fish-edit: закрываем повторное деление до любых спавнов и событий контейнеров.
        parent.Consumed = true;
        SetSuspended(slime, true);

        for (var i = 0; i < offspring; i++)
        {
            var childBreed = SelectChildBreed(parent, breed);
            var child = Spawn(_prototype.Index(childBreed).Mob, coordinates);
            var childState = _slimeQuery.GetComponent(child);
            childState.Generation = parent.Generation + 1;
            childState.MutationChance = parent.MutationChance is <= 0 or >= 1
                ? Math.Clamp(parent.MutationChance, 0, 1)
                : Math.Clamp(parent.MutationChance + _random.NextFloat(-0.025f, 0.025f), 0, 1);
            childState.GrowthRate = Math.Clamp(parent.GrowthRate * _random.NextFloat(0.95f, 1.05f), 0.5f, 2f);
            childState.Offspring = offspring;
            childState.ExtractYield = Math.Clamp(parent.ExtractYield, 1, 3);
            childState.Docility = Math.Clamp(parent.Docility, 0, 1);
            childState.ThermalTolerance = Math.Clamp(parent.ThermalTolerance, 0, 20);
        }

        if (user is { } handler)
            _popup.PopupEntity(Loc.GetString("fish-xeno-divided"), slime, handler);
        QueueDel(slime);
        return true;
    }

    /// <summary>Checks whether a corpse can produce extracts; juveniles and player-controlled specimens cannot.</summary>
    public bool CanHarvest(EntityUid slime)
    {
        // Fish-start: индуцированное ядро нельзя собрать повторно.
        return !TerminatingOrDeleted(slime) && _slimeQuery.TryComp(slime, out var state) &&
               state.Adult && !state.Consumed && _mobState.IsDead(slime) && !HasPlayerMind(slime) &&
               !HasComp<FishXenobiologyIndustrialInductionComponent>(slime);
        // Fish-end
    }

    /// <summary>Claims an adult corpse exactly once. The caller spawns the returned yield and deletes the corpse.</summary>
    public bool TryHarvest(EntityUid slime, out ProtoId<FishXenobiologyBreedPrototype> breed, out int yield)
    {
        breed = default;
        yield = 0;
        if (!CanHarvest(slime) || !_slimeQuery.TryComp(slime, out var state))
            return false;

        state.Consumed = true;
        SetSuspended(slime, true);
        breed = state.Breed;
        yield = Math.Clamp(state.ExtractYield, 1, 3);
        return true;
    }

    /// <summary>Checks whether a genetic treatment can change a living, unconsumed specimen.</summary>
    public bool CanApplyTreatment(EntityUid slime, FishXenobiologyTreatment treatment)
    {
        if (TerminatingOrDeleted(slime) || !_slimeQuery.TryComp(slime, out var state) ||
            state.Consumed || !_mobState.IsAlive(slime))
            return false;

        return treatment switch
        {
            FishXenobiologyTreatment.Mutation => state.MutationChance < 0.999f,
            FishXenobiologyTreatment.Stability => state.MutationChance > 0.001f,
            FishXenobiologyTreatment.Growth => state.GrowthRate < 1.999f,
            FishXenobiologyTreatment.Yield => state.ExtractYield < 3,
            FishXenobiologyTreatment.Fertility => state.Offspring < 4,
            FishXenobiologyTreatment.Docility => state.Docility < 0.999f,
            FishXenobiologyTreatment.Thermal => state.ThermalTolerance < 20f,
            _ => false,
        };
    }

    /// <summary>Applies a bounded, inheritable trait change. The caller consumes the successful treatment item.</summary>
    public bool TryApplyTreatment(EntityUid slime, FishXenobiologyTreatment treatment)
    {
        if (!CanApplyTreatment(slime, treatment) || !_slimeQuery.TryComp(slime, out var state))
            return false;

        switch (treatment)
        {
            case FishXenobiologyTreatment.Mutation:
                state.MutationChance = Math.Min(1, state.MutationChance + 0.1f);
                break;
            case FishXenobiologyTreatment.Stability:
                state.MutationChance = Math.Max(0, state.MutationChance - 0.1f);
                break;
            case FishXenobiologyTreatment.Growth:
                state.GrowthRate = Math.Min(2, state.GrowthRate + 0.15f);
                break;
            case FishXenobiologyTreatment.Yield:
                state.ExtractYield++;
                break;
            case FishXenobiologyTreatment.Fertility:
                state.Offspring++;
                break;
            case FishXenobiologyTreatment.Docility:
                state.Docility = Math.Min(1, state.Docility + 0.25f);
                SetHostile((slime, state), false);
                break;
            case FishXenobiologyTreatment.Thermal:
                state.ThermalTolerance = Math.Min(20, state.ThermalTolerance + 5f);
                break;
        }

        return true;
    }

    /// <summary>Stops development and current HTN actions while a specimen is held inside a container.</summary>
    public void SetSuspended(EntityUid slime, bool suspended)
    {
        if (!_slimeQuery.TryComp(slime, out var state))
            return;

        state.Suspended = suspended;
        if (!_htnQuery.TryComp(slime, out var htn))
            return;

        if (suspended)
        {
            state.ResumeHtn |= htn.Enabled;
            _htn.SetHTNEnabled((slime, htn), false);
        }
        else if (state.ResumeHtn && !state.Consumed)
        {
            state.ResumeHtn = false;
            _htn.SetHTNEnabled((slime, htn), true, 0.2f);
        }
    }

    /// <summary>Completes maturation only when sufficient real nutrition and growth have accumulated.</summary>
    public bool TryMature(EntityUid slime)
    {
        if (!CanMature(slime) || !_slimeQuery.TryComp(slime, out var state))
            return false;

        var breed = _prototype.Index(state.Breed);
        state.Biomass -= breed.GrowthBiomass;
        state.Adult = true;
        RefreshAppearance((slime, state));
        return true;
    }

    /// <summary>Checks maturation without mutating food reserves or the appearance.</summary>
    public bool CanMature(EntityUid slime)
    {
        if (TerminatingOrDeleted(slime) || !_slimeQuery.TryComp(slime, out var state) ||
            state.Adult || state.Consumed || state.Suspended || !_mobState.IsAlive(slime) ||
            _container.IsEntityInContainer(slime))
            return false;

        var breed = _prototype.Index(state.Breed);
        return state.Growth >= breed.GrowthDuration && state.Biomass >= breed.GrowthBiomass &&
               GetHunger(slime) >= MinimumGrowthHunger && IsComfortable(slime, state, breed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;

        var elapsed = _accumulator;
        _accumulator = 0;
        var query = EntityQueryEnumerator<FishXenobiologySlimeComponent, TemperatureComponent>();
        while (query.MoveNext(out var uid, out var state, out var temperature))
        {
            if (state.Consumed || state.Suspended || !_mobState.IsAlive(uid))
                continue;

            var hunger = GetHunger(uid);
            var breed = _prototype.Index(state.Breed);
            var comfortable = IsComfortable(uid, state, breed);

            if (temperature.Temperature < 290.15f)
                state.ColdExposure += elapsed;
            else if (temperature.Temperature > 302.15f)
                state.WarmExposure += elapsed;

            state.FeedingGrace = Math.Max(0, state.FeedingGrace - elapsed);
            SetHostile((uid, state), state.FeedingGrace <= 0 && hunger < 10f * (1f - state.Docility));
            if (!state.Adult && state.Biomass > 0 && hunger >= MinimumGrowthHunger && comfortable)
            {
                state.Growth += elapsed * Math.Clamp(state.GrowthRate, 0.5f, 2f);
                TryMature(uid);
            }

            ApplyEnvironmentalDamage(uid, state, temperature.Temperature, hunger, elapsed);
        }
    }

    private bool IsComfortable(EntityUid uid, FishXenobiologySlimeComponent state, FishXenobiologyBreedPrototype breed)
    {
        return _temperatureQuery.TryComp(uid, out var temperature) &&
               temperature.Temperature >= breed.MinimumTemperature - state.ThermalTolerance &&
               temperature.Temperature <= breed.MaximumTemperature + state.ThermalTolerance;
    }

    private float GetHunger(EntityUid uid)
    {
        return _satiationQuery.TryComp(uid, out var satiation)
            ? _satiation.GetValueOrNull((uid, satiation), SatiationSystem.Hunger) ?? 0f
            : 0f;
    }

    private bool HasPlayerMind(EntityUid uid)
    {
        return TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind;
    }

    private void RefreshAppearance(Entity<FishXenobiologySlimeComponent> ent)
    {
        var dead = _mobState.IsDead(ent);
        var stage = ent.Comp.Adult
            ? dead ? FishXenobiologyStage.DeadAdult : FishXenobiologyStage.Adult
            : dead ? FishXenobiologyStage.DeadBaby : FishXenobiologyStage.Baby;
        _appearance.SetData(ent, FishXenobiologyVisuals.Stage, stage);
    }

    private void SetHostile(Entity<FishXenobiologySlimeComponent> ent, bool hostile)
    {
        if (ent.Comp.Hostile == hostile)
            return;

        ent.Comp.Hostile = hostile;
        _npcFaction.RemoveFaction((ent, null), hostile ? CalmFaction : HungryFaction);
        _npcFaction.AddFaction((ent, null), hostile ? HungryFaction : CalmFaction);

        // Fish-edit: перепланирование само не останавливает уже начатую атаку.
        if (_htnQuery.TryComp(ent, out var htn) && htn.Enabled)
        {
            _htn.SetHTNEnabled((ent, htn), false);
            if (!ent.Comp.Suspended && !ent.Comp.Consumed)
                _htn.SetHTNEnabled((ent, htn), true, 0.2f);
        }
    }

    private void ApplyEnvironmentalDamage(EntityUid uid, FishXenobiologySlimeComponent state, float temperature,
        float hunger, float elapsed)
    {
        var damage = new DamageSpecifier();
        if (temperature < 260.15f - state.ThermalTolerance)
            damage.DamageDict[ColdDamage] = FixedPoint2.New(elapsed * 0.5f);
        if (temperature > 330.15f + state.ThermalTolerance)
            damage.DamageDict[HeatDamage] = FixedPoint2.New(elapsed * 0.5f);
        if (hunger <= 0)
            damage.DamageDict[StarvationDamage] = FixedPoint2.New(elapsed * 0.1f);
        if (!damage.Empty)
            _damageable.TryChangeDamage((uid, null), damage, interruptsDoAfters: false);
    }

    private ProtoId<FishXenobiologyBreedPrototype> SelectChildBreed(FishXenobiologySlimeComponent parent,
        FishXenobiologyBreedPrototype breed)
    {
        if (parent.MutationChance >= 0.999f && breed.Tier >= 3)
            return Rainbow;

        var totalFood = parent.ProteinIntake + parent.NutrimentIntake;
        var mixedDiet = totalFood > 0
            ? Math.Min(parent.ProteinIntake, parent.NutrimentIntake) / totalFood
            : 0f;
        var thermalExposure = parent.ColdExposure + parent.WarmExposure;
        var chance = Math.Clamp(parent.MutationChance *
                               (1f + mixedDiet * 0.2f + Math.Min(0.1f, thermalExposure / 3600f)), 0, 1);
        if (!_random.Prob(chance) || breed.Mutations.Count == 0)
            return parent.Breed;

        var weightTotal = 0f;
        foreach (var (candidate, weight) in breed.Mutations)
            weightTotal += GetMutationWeight(candidate, weight, parent);
        if (weightTotal <= 0)
            return parent.Breed;

        var roll = _random.NextFloat() * weightTotal;
        foreach (var (candidate, weight) in breed.Mutations)
        {
            roll -= GetMutationWeight(candidate, weight, parent);
            if (roll <= 0)
                return candidate;
        }

        return parent.Breed;
    }

    private static float GetMutationWeight(ProtoId<FishXenobiologyBreedPrototype> candidate, float weight,
        FishXenobiologySlimeComponent parent)
    {
        var coldBiased = candidate.Id is "FishBlue" or "FishDarkBlue" or "FishSilver" or "FishCerulean" or "FishSepia";
        var warmBiased = candidate.Id is "FishOrange" or "FishYellow" or "FishRed" or "FishOil" or "FishDarkPurple";
        var proteinBiased = candidate.Id is "FishRed" or "FishGreen" or "FishDarkPurple" or "FishGold";
        var plantBiased = candidate.Id is "FishPurple" or "FishPink" or "FishGreen" or "FishLightPink";
        var adjusted = Math.Max(0, weight);
        if (coldBiased && parent.ColdExposure > parent.WarmExposure + 30f ||
            warmBiased && parent.WarmExposure > parent.ColdExposure + 30f)
            adjusted *= 2f;
        if (proteinBiased && parent.ProteinIntake > parent.NutrimentIntake * 1.5f ||
            plantBiased && parent.NutrimentIntake > parent.ProteinIntake * 1.5f)
            adjusted *= 1.5f;
        return adjusted;
    }
}
