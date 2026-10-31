using Content.Server.Ghost.Roles.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Shared.Mobs.Components;

namespace Content.Server._Fish.Salvage;

/// <summary>
/// Правила для мобов экспедиции: они замечают игроков издалека и никогда не становятся гост-ролями.
/// </summary>
public sealed partial class ExpeditionMobSystem : EntitySystem
{
    [Dependency] private IEntityManager _entManager = default!;

    /// <summary>
    /// Во сколько раз увеличиваются радиусы обзора и агра мобов экспедиции.
    /// </summary>
    public const float RadiusMultiplier = 1.5f;

    /// <summary>
    /// Радиус, выше которого множитель уже не применяется: у пиратов радиусы и так большие.
    /// </summary>
    public const float RadiusCap = 35f;

    private const string VisionRadiusKey = "VisionRadius";
    private const string AggroVisionRadiusKey = "AggroVisionRadius";

    /// <summary>
    /// Увеличивает радиусы обзора и агра у всех мобов с ИИ на карте экспедиции, чтобы они замечали игроков издалека
    /// и сразу атаковали, а не стояли на месте.
    /// Вызывать нужно один раз, после того как все мобы появились. Карта в этот момент ещё на паузе,
    /// поэтому перебираем в том числе приостановленные сущности.
    /// </summary>
    public void BoostMobVision(EntityUid mapUid)
    {
        var query = AllEntityQuery<HTNComponent, TransformComponent>();
        while (query.MoveNext(out _, out var htn, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            ScaleRadius(htn.Blackboard, VisionRadiusKey);
            ScaleRadius(htn.Blackboard, AggroVisionRadiusKey);
        }
    }

    /// <summary>
    /// Снимает гост-роль и возможность захвата у всех мобов на карте экспедиции: у боссов с гарантированной ролью,
    /// у мобов с шансом на роль и у тех, кого создали спавнеры. Спавнеры гост-ролей, которые не являются мобами,
    /// не затрагиваются. Карта в этот момент ещё на паузе, поэтому перебираем в том числе приостановленные сущности.
    /// </summary>
    public void RemoveGhostRoles(EntityUid mapUid)
    {
        var roles = AllEntityQuery<GhostRoleComponent, MobStateComponent, TransformComponent>();
        while (roles.MoveNext(out var uid, out _, out _, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            RemCompDeferred<GhostRoleComponent>(uid);
        }

        // Возможность захвата снимаем отдельно: гост-роль могла уже удалиться (например, при prob: 0 на MapInit),
        // а захват остаться.
        var takeovers = AllEntityQuery<GhostTakeoverAvailableComponent, MobStateComponent, TransformComponent>();
        while (takeovers.MoveNext(out var uid, out _, out _, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            RemCompDeferred<GhostTakeoverAvailableComponent>(uid);
        }
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
}
