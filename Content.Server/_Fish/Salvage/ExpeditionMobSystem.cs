using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Salvage.Expeditions;
using Content.Shared._Fish.NPC;
using Content.Shared.Ghost.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Salvage;

/// <summary>
/// Политика NPC на экспедициях: vision boost + proximity wake/sleep вместо always-awake.
/// </summary>
public sealed partial class ExpeditionMobSystem : EntitySystem
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    /// <summary>
    /// Во сколько раз увеличиваются радиусы обзора и агра мобов экспедиции.
    /// </summary>
    public const float RadiusMultiplier = 1.5f;

    /// <summary>
    /// Радиус, выше которого множитель уже не применяется: у пиратов радиусы и так большие.
    /// </summary>
    public const float RadiusCap = 35f;

    /// <summary>
    /// Дистанция wake на экспедиции (больше station sleep, меньше always-on всей карты).
    /// </summary>
    public const float ExpeditionWakeDistance = 40f;

    /// <summary>
    /// Гистерезис: sleep только если игрок дальше wake + hysteresis.
    /// </summary>
    public const float ExpeditionSleepHysteresis = 10f;

    private const string VisionRadiusKey = "VisionRadius";
    private const string AggroVisionRadiusKey = "AggroVisionRadius";
    private const string VisionBoostedKey = "FishExpeditionVisionBoosted";

    private TimeSpan _nextCheckTime = TimeSpan.Zero;
    private static readonly TimeSpan CheckCooldown = TimeSpan.FromSeconds(5);

    [Dependency] private EntityQuery<ActorComponent> _actorQuery = default!;
    [Dependency] private EntityQuery<ActiveNPCComponent> _activeQuery = default!;
    [Dependency] private EntityQuery<GhostComponent> _ghostQuery = default!;
    [Dependency] private EntityQuery<SalvageExpeditionComponent> _expeditionQuery = default!;

    private readonly HashSet<Entity<ActorComponent>> _players = [];

    /// <summary>
    /// Хук из <see cref="NPCSystem.OnNPCMapInit"/> — на экспе поднимаем vision (без второй подписки на MapInit).
    /// </summary>
    public void TryBoostOnMapInit(EntityUid uid, HTNComponent htn)
    {
        if (Transform(uid).MapUid is not { } mapUid || !_expeditionQuery.HasComp(mapUid))
            return;

        ApplyVisionBoost(htn.Blackboard);
        if (TryComp<FishNpcBrainComponent>(uid, out var brain))
            ApplyBrainVisionBoost(brain);
    }

    /// <summary>
    /// Увеличивает радиусы обзора и агра у всех мобов с ИИ на карте экспедиции.
    /// Вызывать после спавна (карта может быть на паузе) — дополняет MapInit для уже созданных HTN.
    /// </summary>
    public void BoostMobVision(EntityUid mapUid)
    {
        var query = AllEntityQuery<HTNComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var htn, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            ApplyVisionBoost(htn.Blackboard);
            if (TryComp<FishNpcBrainComponent>(uid, out var brain))
                ApplyBrainVisionBoost(brain);
        }
    }

    public void ApplyVisionBoost(NPCBlackboard blackboard)
    {
        // Идемпотентно: MapInit + пост-спавн BoostMobVision не должны умножать дважды.
        if (blackboard.ContainsKey(VisionBoostedKey))
            return;

        ScaleRadius(blackboard, VisionRadiusKey);
        ScaleRadius(blackboard, AggroVisionRadiusKey);
        blackboard.SetValue(VisionBoostedKey, true);
    }

    /// <summary>
    /// Буст радиусов FishNpcBrain на экспедиции (отдельно от HTN blackboard).
    /// </summary>
    public void ApplyBrainVisionBoost(FishNpcBrainComponent brain)
    {
        if (brain.ExpeditionVisionBoosted)
            return;

        brain.VisionRadius = ScaleRadiusValue(brain.VisionRadius);
        brain.AggroVisionRadius = ScaleRadiusValue(brain.AggroVisionRadius);
        brain.ExpeditionVisionBoosted = true;
    }

    private static float ScaleRadiusValue(float radius)
    {
        return MathF.Min(radius * RadiusMultiplier, MathF.Max(radius, RadiusCap));
    }

    /// <summary>
    /// Умножает радиус на <see cref="RadiusMultiplier"/>, не поднимая его выше <see cref="RadiusCap"/>
    /// (если радиус уже больше предела, он остаётся прежним).
    /// </summary>
    private void ScaleRadius(NPCBlackboard blackboard, string key)
    {
        var radius = blackboard.GetValueOrDefault<float>(key, _entManager);
        var scaled = MathF.Min(radius * RadiusMultiplier, MathF.Max(radius, RadiusCap));
        blackboard.SetValue(key, scaled);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheckTime)
            return;

        _nextCheckTime = _timing.CurTime + CheckCooldown;

        var query = EntityQueryEnumerator<HTNComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var htn, out var xform))
        {
            if (xform.MapUid is not { } mapUid || !_expeditionQuery.HasComp(mapUid))
                continue;

            if (!_mobState.IsAlive(uid))
                continue;

            if (_actorQuery.HasComp(uid))
                continue;

            var isActive = _activeQuery.HasComponent(uid);
            var nearPlayer = HasPlayerInRange(uid, xform, isActive
                ? ExpeditionWakeDistance + ExpeditionSleepHysteresis
                : ExpeditionWakeDistance);

            if (nearPlayer)
            {
                if (!isActive)
                    _npc.WakeNPC(uid, htn);
            }
            else if (isActive)
            {
                _npc.SleepNPC(uid, htn);
            }
        }
    }

    private bool HasPlayerInRange(EntityUid uid, TransformComponent xform, float distance)
    {
        var npcCoords = _transform.GetMapCoordinates(uid, xform);
        _players.Clear();
        _lookup.GetEntitiesInRange(npcCoords,
            distance,
            _players,
            LookupFlags.Dynamic | LookupFlags.Approximate);

        foreach (var ent in _players)
        {
            if (_ghostQuery.HasComp(ent))
                continue;

            return true;
        }

        return false;
    }
}
