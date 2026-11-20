using System.Numerics;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Fish.NPC;
using Content.Shared.CombatMode;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Fish.NPC;

/// <summary>
/// Режимный мозг враждебных NPC: штурм/оборона как у обученных операторов.
/// </summary>
public sealed partial class FishNpcBrainSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private NPCSteeringSystem _steering = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private GunSystem _gun = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedCombatModeSystem _combatMode = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery = default!;
    [Dependency] private EntityQuery<DamageableComponent> _damageQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishNpcBrainComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FishNpcBrainComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<FishNpcBrainComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.DisableHtn && TryComp<HTNComponent>(ent, out var htn))
            _htn.SetHTNEnabled((ent.Owner, htn), false);

        ent.Comp.ModeChangedAt = _timing.CurTime;
        ent.Comp.NextModeEvaluate = _timing.CurTime;
    }

    private void OnShutdown(Entity<FishNpcBrainComponent> ent, ref ComponentShutdown args)
    {
        ClearCombat(ent.Owner);
        _steering.Unregister(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<FishNpcBrainComponent, ActiveNPCComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var brain, out _, out var xform))
        {
            if (_mobStateQuery.TryComp(uid, out var mobState) && mobState.CurrentState != MobState.Alive)
            {
                ClearCombat(uid);
                continue;
            }

            if (curTime >= brain.NextModeEvaluate)
            {
                brain.NextModeEvaluate = curTime + brain.ModeEvaluateInterval;
                EvaluateMode(uid, brain, xform, curTime);
            }

            ExecuteMode(uid, brain, xform, frameTime, curTime);
        }
    }

    private void EvaluateMode(EntityUid uid, FishNpcBrainComponent brain, TransformComponent xform, TimeSpan curTime)
    {
        RefreshTarget(uid, brain, xform);

        var dist = GetTargetDistance(uid, brain, xform);
        var hasGun = TryGetAmmoFraction(uid, out var ammoFrac);
        var healthFrac = GetHealthFraction(uid);
        var los = brain.Target != null && HasLos(uid, brain.Target.Value, xform);

        if (brain.Target != null && los && _xformQuery.TryComp(brain.Target.Value, out var targetXform))
        {
            brain.LastSeenCoordinates = targetXform.Coordinates;
            brain.SearchAccumulator = 0f;
        }

        var assault = brain.Doctrine == FishNpcDoctrine.Assault;
        var meleeCommit = assault ? brain.AssaultMeleeCommitDistance : brain.MeleeCommitDistance;
        var fleeThreshold = assault ? brain.FleeHealthFraction : brain.FleeHealthFraction + 0.12f;

        var d = dist ?? float.MaxValue;
        var scores = new float[7];
        scores[(int) FishNpcMode.Idle] = brain.Target == null && brain.LastSeenCoordinates == null ? 1.2f : 0.05f;
        scores[(int) FishNpcMode.Hunt] = brain.Target != null && d > 12f ? 1.4f : (brain.Target != null ? 0.6f : 0.1f);
        scores[(int) FishNpcMode.EngageRanged] = hasGun && ammoFrac > 0.001f && brain.Target != null && d >= meleeCommit
            ? 1.5f + (los ? 0.4f : -0.5f)
            : 0f;
        scores[(int) FishNpcMode.EngageMelee] = brain.Target != null && d <= meleeCommit + 1.5f
            ? 1.3f + (assault ? 0.5f : 0.1f)
            : (assault && brain.Target != null && d < 8f ? 0.7f : 0f);
        scores[(int) FishNpcMode.Reload] = hasGun && ammoFrac <= 0.001f ? 2.2f : 0f;
        scores[(int) FishNpcMode.Search] = brain.Target == null && brain.LastSeenCoordinates != null
            ? 1.3f
            : (!los && brain.Target != null ? 1.0f : 0f);
        scores[(int) FishNpcMode.Flee] = healthFrac <= fleeThreshold && brain.Target != null ? 2.5f : 0f;

        if (!assault)
        {
            // Оборона: держать коридор дистанции, раньше reload/flee.
            if (hasGun && ammoFrac > 0.001f && brain.Target != null &&
                d >= brain.DefenseHoldMin && d <= brain.DefenseHoldMax && los)
                scores[(int) FishNpcMode.EngageRanged] += 0.8f;
            if (brain.Target != null && d < brain.DefenseHoldMin)
                scores[(int) FishNpcMode.EngageRanged] += 0.3f; // kite via execute
            scores[(int) FishNpcMode.Reload] += hasGun && ammoFrac < 0.25f ? 0.4f : 0f;
            scores[(int) FishNpcMode.EngageMelee] *= 0.65f;
        }
        else
        {
            scores[(int) FishNpcMode.Hunt] += 0.35f;
            scores[(int) FishNpcMode.EngageMelee] += 0.35f;
        }

        // Sticky current mode
        scores[(int) brain.Mode] += 0.35f;

        var best = FishNpcMode.Idle;
        var bestScore = float.MinValue;
        for (var i = 0; i < scores.Length; i++)
        {
            if (scores[i] <= bestScore)
                continue;
            bestScore = scores[i];
            best = (FishNpcMode) i;
        }

        if (best == brain.Mode)
            return;

        if (curTime - brain.ModeChangedAt < brain.ModeHysteresis &&
            best is not (FishNpcMode.Reload or FishNpcMode.Flee))
            return;

        SetMode(uid, brain, best, curTime);
    }

    private void SetMode(EntityUid uid, FishNpcBrainComponent brain, FishNpcMode mode, TimeSpan curTime)
    {
        if (brain.Mode is FishNpcMode.EngageMelee or FishNpcMode.EngageRanged)
            ClearCombat(uid);

        brain.Mode = mode;
        brain.ModeChangedAt = curTime;
        Dirty(uid, brain);
    }

    private void ExecuteMode(EntityUid uid, FishNpcBrainComponent brain, TransformComponent xform, float frameTime, TimeSpan curTime)
    {
        switch (brain.Mode)
        {
            case FishNpcMode.Idle:
                _steering.Unregister(uid);
                ClearCombat(uid);
                break;

            case FishNpcMode.Hunt:
                ClearCombat(uid);
                if (brain.Target != null)
                    _steering.Register(uid, new EntityCoordinates(brain.Target.Value, Vector2.Zero));
                else if (brain.LastSeenCoordinates is { } last)
                    _steering.Register(uid, last);
                break;

            case FishNpcMode.EngageRanged:
                ExecuteRanged(uid, brain, xform);
                break;

            case FishNpcMode.EngageMelee:
                ExecuteMelee(uid, brain);
                break;

            case FishNpcMode.Reload:
                ClearCombat(uid);
                if (!TryReload(uid))
                {
                    // Не удалось — ищем цель в мили или hunt.
                    SetMode(uid, brain, brain.Target != null ? FishNpcMode.EngageMelee : FishNpcMode.Hunt, curTime);
                }
                break;

            case FishNpcMode.Search:
                ClearCombat(uid);
                brain.SearchAccumulator += frameTime;
                if (brain.LastSeenCoordinates is { } searchCoords)
                    _steering.Register(uid, searchCoords);
                if (brain.SearchAccumulator >= brain.SearchTimeoutSeconds)
                {
                    brain.LastSeenCoordinates = null;
                    brain.SearchAccumulator = 0f;
                    SetMode(uid, brain, FishNpcMode.Idle, curTime);
                }
                break;

            case FishNpcMode.Flee:
                ClearCombat(uid);
                if (brain.Target != null && _xformQuery.TryComp(brain.Target.Value, out var targetXform))
                {
                    var myPos = _transform.GetWorldPosition(xform);
                    var theirPos = _transform.GetWorldPosition(targetXform);
                    var away = myPos - theirPos;
                    if (away.LengthSquared() < 0.01f)
                        away = new Vector2(1f, 0f);
                    away = away.Normalized() * 8f;
                    var fleeCoords = xform.Coordinates.Offset(away);
                    _steering.Register(uid, fleeCoords);
                }
                break;
        }
    }

    private void ExecuteRanged(EntityUid uid, FishNpcBrainComponent brain, TransformComponent xform)
    {
        if (brain.Target is not { } target || !Exists(target))
            return;

        var dist = GetTargetDistance(uid, brain, xform) ?? 999f;
        var assault = brain.Doctrine == FishNpcDoctrine.Assault;

        // Оборона: отскок если враг слишком близко. Штурм: давить вперёд.
        if (!assault && dist < brain.DefenseHoldMin && _xformQuery.TryComp(target, out var tx))
        {
            var myPos = _transform.GetWorldPosition(xform);
            var theirPos = _transform.GetWorldPosition(tx);
            var away = (myPos - theirPos).Normalized() * 6f;
            _steering.Register(uid, xform.Coordinates.Offset(away));
        }
        else if (assault || dist > brain.PreferRangedDistance)
        {
            _steering.Register(uid, new EntityCoordinates(target, Vector2.Zero));
        }
        else if (!assault && dist > brain.DefenseHoldMax)
        {
            _steering.Register(uid, new EntityCoordinates(target, Vector2.Zero));
        }
        else
        {
            // В коридоре — стоп движения, только стрельба.
            _steering.Unregister(uid);
        }

        var ranged = EnsureComp<NPCRangedCombatComponent>(uid);
        ranged.Target = target;
        if (TryComp<CombatModeComponent>(uid, out var combat))
            _combatMode.SetInCombatMode(uid, true, combat);
    }

    private void ExecuteMelee(EntityUid uid, FishNpcBrainComponent brain)
    {
        if (brain.Target is not { } target || !Exists(target))
            return;

        RemComp<NPCRangedCombatComponent>(uid);
        var melee = EnsureComp<NPCMeleeCombatComponent>(uid);
        melee.Target = target;
        if (TryComp<CombatModeComponent>(uid, out var combat))
            _combatMode.SetInCombatMode(uid, true, combat);
        _steering.Register(uid, new EntityCoordinates(target, Vector2.Zero));
    }

    private void RefreshTarget(EntityUid uid, FishNpcBrainComponent brain, TransformComponent xform)
    {
        if (brain.Target != null)
        {
            if (!Exists(brain.Target.Value) ||
                (_mobStateQuery.TryComp(brain.Target.Value, out var state) && state.CurrentState != MobState.Alive))
            {
                brain.Target = null;
            }
            else
            {
                var dist = GetTargetDistance(uid, brain, xform);
                var vision = brain.AggroVisionRadius;
                if (dist is null || dist > vision)
                {
                    // Не сбрасываем мгновенно — Search/last-seen.
                    if (dist is null || dist > vision * 1.35f)
                        brain.Target = null;
                }
                else
                {
                    return; // держим текущую цель
                }
            }
        }

        EntityUid? best = null;
        var bestDist = float.MaxValue;
        foreach (var hostile in _faction.GetNearbyHostiles(uid, brain.AggroVisionRadius))
        {
            if (!_xformQuery.TryComp(hostile, out var hx))
                continue;
            if (!xform.Coordinates.TryDistance(EntityManager, hx.Coordinates, out var d))
                continue;
            if (d >= bestDist)
                continue;
            bestDist = d;
            best = hostile;
        }

        brain.Target = best;
        if (best != null && _xformQuery.TryComp(best.Value, out var bx))
            brain.LastSeenCoordinates = bx.Coordinates;
    }

    private float? GetTargetDistance(EntityUid uid, FishNpcBrainComponent brain, TransformComponent xform)
    {
        if (brain.Target is not { } target || !_xformQuery.TryComp(target, out var tx))
            return null;
        if (!xform.Coordinates.TryDistance(EntityManager, tx.Coordinates, out var d))
            return null;
        return d;
    }

    private bool HasLos(EntityUid uid, EntityUid target, TransformComponent xform)
    {
        if (!_xformQuery.TryComp(target, out var tx))
            return false;
        return _interaction.InRangeUnobstructed(uid, target, range: 40f);
    }

    private bool TryGetAmmoFraction(EntityUid uid, out float fraction)
    {
        fraction = 0f;
        if (!_gun.TryGetGun(uid, out var gun))
            return false;

        var ev = new GetAmmoCountEvent();
        RaiseLocalEvent(gun.Owner, ref ev);
        if (ev.Capacity <= 0)
        {
            fraction = 1f; // infinite / unknown — treat as ready
            return true;
        }

        fraction = ev.Count / (float) ev.Capacity;
        return true;
    }

    private float GetHealthFraction(EntityUid uid)
    {
        if (!_damageQuery.TryComp(uid, out var damageable))
            return 1f;

        if (!_thresholds.TryGetThresholdForState(uid, MobState.Critical, out var crit) &&
            !_thresholds.TryGetThresholdForState(uid, MobState.Dead, out crit))
            return 1f;

        if (crit <= 0)
            return 1f;

        var total = _damageable.GetTotalDamage((uid, damageable));
        var ratio = (float) (total / crit);
        return Math.Clamp(1f - ratio, 0f, 1f);
    }

    private bool TryReload(EntityUid uid)
    {
        if (!_gun.TryGetGun(uid, out var gun))
            return false;

        if (HasComp<MagazineAmmoProviderComponent>(gun.Owner) ||
            HasComp<ChamberMagazineAmmoProviderComponent>(gun.Owner))
        {
            foreach (var item in _inventory.GetHandOrInventoryEntities(uid))
            {
                if (item == gun.Owner)
                    continue;
                if (_slots.TryInsert(gun.Owner, SharedGunSystem.MagazineSlot, item, uid, excludeUserAudio: true))
                    return true;
            }
        }

        if (TryComp(gun.Owner, out BallisticAmmoProviderComponent? ballistic))
        {
            var coords = Transform(gun.Owner).Coordinates;
            foreach (var item in _inventory.GetHandOrInventoryEntities(uid))
            {
                if (item == gun.Owner)
                    continue;
                if (_whitelist.IsWhitelistFail(ballistic.Whitelist, item))
                    continue;
                _interaction.InteractUsing(uid, item, gun.Owner, coords, checkCanInteract: false, checkCanUse: false);
                return true;
            }
        }

        return false;
    }

    private void ClearCombat(EntityUid uid)
    {
        RemComp<NPCMeleeCombatComponent>(uid);
        RemComp<NPCRangedCombatComponent>(uid);
    }
}
