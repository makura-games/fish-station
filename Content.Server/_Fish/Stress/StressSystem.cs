using System;
using System.Collections.Generic;
using Content.Server._Fish.MentalHealth;
using Content.Server.Roles.Jobs;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared._Fish.MentalHealth;
using Content.Shared._Fish.Neuro;
using Content.Shared._Fish.Stress;
using Content.Shared.Fluids.Components;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Medical;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Stress;

public sealed partial class StressSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery = default!;
    [Dependency] private EntityQuery<NpcFactionMemberComponent> _factionQuery = default!;
    [Dependency] private EntityQuery<PuddleComponent> _puddleQuery = default!;
    [Dependency] private JobSystem _jobs = default!;
    [Dependency] private MentalHealthConfigSystem _config = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private VomitSystem _vomit = default!;
    [Dependency] private IGameTiming _timing = default!;

    private float _updateTimer;
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _seenCorpses = new();
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _seenBloodPuddles = new();
    private readonly Dictionary<EntityUid, int> _seenBloodPuddleCounts = new();
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _nearbyHostiles = new();
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _currentHostiles = new();
    private readonly Dictionary<EntityUid, Dictionary<EntityUid, TimeSpan>> _startledHostiles = new();
    private readonly Dictionary<EntityUid, TimeSpan> _highStressStarted = new();
    private readonly Dictionary<EntityUid, TimeSpan> _vomitCooldowns = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StressComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StressComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<GunComponent, GunGetAmmoSpreadEvent>(OnGunGetAmmoSpread);
    }

    /// <summary>
    /// Увеличивает разброс оружия стрелка с высоким уровнем стресса.
    /// </summary>
    private void OnGunGetAmmoSpread(Entity<GunComponent> ent, ref GunGetAmmoSpreadEvent args)
    {
        if (args.Shooter is not { } shooter ||
            !TryComp<StressComponent>(shooter, out var stress) ||
            stress.Current <= MentalHealthConstants.StressThresholdHigh)
            return;

        args.Spread *= 1.35f;
    }

    /// <summary>
    /// Применяет сопротивления стрессу, заданные для текущей роли сущности.
    /// </summary>
    private void OnStartup(Entity<StressComponent> ent, ref ComponentStartup args)
    {
        ApplyRoleResists(ent, ent.Comp);
    }

    /// <summary>
    /// Применяет сопротивления стрессу после выбора роли игрока.
    /// </summary>
    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (TryComp<StressComponent>(ev.Mob, out var stress))
            ApplyRoleResists(ev.Mob, stress, ev.JobId);
    }

    /// <summary>
    /// Удаляет временные данные стресса после снятия компонента.
    /// </summary>
    private void OnShutdown(Entity<StressComponent> ent, ref ComponentShutdown args)
    {
        var uid = ent.Owner;
        _seenCorpses.Remove(uid);
        _seenBloodPuddles.Remove(uid);
        _seenBloodPuddleCounts.Remove(uid);
        _nearbyHostiles.Remove(uid);
        _currentHostiles.Remove(uid);
        _startledHostiles.Remove(uid);
        _highStressStarted.Remove(uid);
        _vomitCooldowns.Remove(uid);
        RemComp<StressTremorComponent>(uid);
    }

    /// <summary>
    /// Назначает сопротивления стрессорам на основе роли сущности.
    /// </summary>
    private void ApplyRoleResists(EntityUid entity, StressComponent stress, string? jobId = null)
    {
        var roleId = jobId;
        if (roleId == null &&
            TryComp<MindContainerComponent>(entity, out var mindContainer) &&
            mindContainer.Mind is { } mindUid &&
            TryComp<MindComponent>(mindUid, out var mind))
        {
            if (_jobs.MindTryGetJob(mindUid, out var job))
                roleId = job.ID;
            else if (mind.RoleType.Id != "Neutral")
                roleId = "Antag";
        }

        if (!_config.StressResists.TryGetValue(roleId ?? string.Empty, out var resists) &&
            !_config.StressResists.TryGetValue("default", out resists))
            resists = (0f, 0f);

        stress.ResistBlood = resists.blood;
        stress.ResistEnemies = resists.enemies;
        Dirty(entity, stress);
    }

    /// <summary>
    /// Обновляет стресс от травмирующих источников и враждебных существ поблизости.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;
        if (_updateTimer < 0.5f)
            return;

        var elapsed = _updateTimer;
        _updateTimer = 0f;

        var query = EntityQueryEnumerator<StressComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var stress, out var xform))
        {
            var wasHigh = stress.Current > MentalHealthConstants.StressThresholdHigh;
            var stressDelta = 0f;
            var nearby = _lookup.GetEntitiesInRange(xform.Coordinates, 7f);
            var currentHostiles = GetSeenSet(_currentHostiles, uid);
            currentHostiles.Clear();
            _factionQuery.TryGetComponent(uid, out var observerFactions);
            var now = _timing.CurTime;
            var sawTraumaSource = false;

            foreach (var target in nearby)
            {
                if (target == uid)
                    continue;

                var isDead = _mobStateQuery.TryGetComponent(target, out var mobState) &&
                             mobState.CurrentState == MobState.Dead;
                var isHostile = observerFactions != null &&
                                _factionQuery.TryGetComponent(target, out var targetFactions) &&
                                IsHostile(observerFactions, target, targetFactions);
                var hasBloodPuddle = false;
                if (_puddleQuery.TryGetComponent(target, out var puddle) &&
                    _solutions.TryGetSolution(target, puddle.SolutionName, out _, out var solution))
                {
                    foreach (var reagent in solution.Contents)
                    {
                        if (reagent.Reagent.Prototype != "Blood" || reagent.Quantity <= 0)
                            continue;

                        hasBloodPuddle = true;
                        break;
                    }
                }

                if ((!isDead && !isHostile && !hasBloodPuddle) ||
                    !_interaction.InRangeUnobstructed(uid, target, 7f))
                    continue;

                if (isDead)
                {
                    sawTraumaSource = true;
                    stressDelta += GetStressFromSource(2f, stress.ResistBlood, stress.AdaptationBlood);
                    if (GetSeenSet(_seenCorpses, uid).Add(target))
                        AddBloodAdaptation(uid, stress, 1f);
                }

                if (hasBloodPuddle)
                {
                    sawTraumaSource = true;
                    stressDelta += GetStressFromSource(2f, stress.ResistBlood, stress.AdaptationBlood);
                    if (GetSeenSet(_seenBloodPuddles, uid).Add(target))
                    {
                        var count = _seenBloodPuddleCounts.GetValueOrDefault(uid) + 1;
                        _seenBloodPuddleCounts[uid] = count;
                        if (count % 3 == 0)
                            AddBloodAdaptation(uid, stress, 1f);
                    }
                }

                if (!isHostile || isDead)
                    continue;

                currentHostiles.Add(target);
                var previousHostiles = GetSeenSet(_nearbyHostiles, uid);
                if (!previousHostiles.Contains(target))
                {
                    if (!_startledHostiles.TryGetValue(uid, out var startled))
                    {
                        startled = new Dictionary<EntityUid, TimeSpan>();
                        _startledHostiles.Add(uid, startled);
                    }

                    startled[target] = now + TimeSpan.FromSeconds(2);
                }

                var multiplier = 1f;
                if (_startledHostiles.TryGetValue(uid, out var startledUntil) &&
                    startledUntil.TryGetValue(target, out var expires))
                {
                    if (now < expires)
                        multiplier = 2f;
                    else
                        startledUntil.Remove(target);
                }

                stressDelta += GetStressFromSource(3f * multiplier, stress.ResistEnemies, stress.AdaptationEnemies);
            }

            var seenHostiles = GetSeenSet(_nearbyHostiles, uid);
            seenHostiles.Clear();
            seenHostiles.UnionWith(currentHostiles);
            stress.Current = Math.Clamp(
                stress.Current + stressDelta * elapsed,
                0f,
                MentalHealthConstants.StressMax);

            var isHigh = stress.Current > MentalHealthConstants.StressThresholdHigh;
            if (isHigh)
            {
                var tremor = EnsureComp<StressTremorComponent>(uid);
                tremor.Intensity = Math.Clamp(
                    (stress.Current - MentalHealthConstants.StressThresholdHigh) /
                    (MentalHealthConstants.StressMax - MentalHealthConstants.StressThresholdHigh),
                    0f,
                    1f);
            }
            else
            {
                RemComp<StressTremorComponent>(uid);
            }

            if (wasHigh != isHigh)
                RaiseNetworkEvent(new StressEffectsEvent(GetNetEntity(uid), isHigh), Filter.Entities(uid));

            UpdateHighStressEffects(uid, stress.Current, now, sawTraumaSource);
            Dirty(uid, stress);
        }
    }

    /// <summary>
    /// Возвращает сохранённый набор наблюдавшихся сущностей, создавая его при необходимости.
    /// </summary>
    private static HashSet<EntityUid> GetSeenSet(
        Dictionary<EntityUid, HashSet<EntityUid>> seenByEntity,
        EntityUid uid)
    {
        if (seenByEntity.TryGetValue(uid, out var seen))
            return seen;

        seen = new HashSet<EntityUid>();
        seenByEntity.Add(uid, seen);
        return seen;
    }

    private bool IsHostile(
        NpcFactionMemberComponent observerFactions,
        EntityUid target,
        NpcFactionMemberComponent targetFactions)
    {
        foreach (var faction in observerFactions.Factions)
        {
            if (_npcFaction.IsFactionHostile(faction, (target, targetFactions)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Увеличивает адаптацию к крови и применяет связанную постоянную потерю нейронов.
    /// </summary>
    private void AddBloodAdaptation(EntityUid uid, StressComponent stress, float amount)
    {
        var totalAdaptation = stress.AdaptationBlood + stress.AdaptationEnemies;
        var maximumAdaptation = MathF.Min(stress.AdaptationMax, 20f);
        var adaptation = MathF.Min(amount, MathF.Max(0f, maximumAdaptation - totalAdaptation));
        if (adaptation <= 0f)
            return;

        stress.AdaptationBlood += adaptation;
        ApplyAdaptationNeuroLoss(uid, stress, adaptation);
    }

    /// <summary>
    /// Обрабатывает рвоту при продолжительном высоком стрессе от травмирующих источников.
    /// </summary>
    private void UpdateHighStressEffects(EntityUid uid, float stress, TimeSpan now, bool sawTraumaSource)
    {
        if (stress <= MentalHealthConstants.StressThresholdHigh || !sawTraumaSource)
        {
            _highStressStarted.Remove(uid);
            return;
        }

        if (!_highStressStarted.TryGetValue(uid, out var started))
        {
            _highStressStarted[uid] = now;
            return;
        }

        if (now - started < TimeSpan.FromSeconds(1.5) ||
            (_vomitCooldowns.TryGetValue(uid, out var cooldown) && now < cooldown))
            return;

        _vomit.Vomit(uid);
        _vomitCooldowns[uid] = now + TimeSpan.FromSeconds(60);
        _highStressStarted[uid] = now;
    }

    private static float GetStressFromSource(float baseStress, float resist, float adaptation)
    {
        var totalResist = Math.Clamp(resist + adaptation, 0f, 100f);
        return baseStress * (1f - totalResist / 100f);
    }

    /// <summary>
    /// Переносит часть адаптации в постоянную потерю нейронов, не превышая их максимум.
    /// </summary>
    private void ApplyAdaptationNeuroLoss(EntityUid uid, StressComponent stress, float adaptation)
    {
        if (adaptation <= 0f || !TryComp<NeuroComponent>(uid, out var neuro))
            return;

        neuro.PermanentLoss = MathF.Min(
            neuro.PermanentLoss + adaptation * stress.AdaptationCost,
            neuro.Max);
        neuro.Current = MathF.Min(neuro.Current, neuro.Max - neuro.PermanentLoss);
        Dirty(uid, neuro);
    }
}
