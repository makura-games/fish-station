using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Fish.PerformanceMetrics;

/// <summary>
/// Учёт обращений к оружию. Сам выстрел сервер уже разрешает; нам важно только то,
/// сколько попыток было и сколько из них дошло до цели.
/// </summary>
public sealed partial class PmAttemptSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PmSystem _pm = default!;

    private EntityQuery<ActorComponent> _actorQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();

        SubscribeLocalEvent<GunComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnShotAttempted(Entity<GunComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!_pm.Enabled)
            return;

        if (!_actorQuery.TryComp(args.User, out var actor) ||
            actor.PlayerSession.Status != SessionStatus.InGame)
        {
            return;
        }

        var pm = _pm.GetSession(actor.PlayerSession.UserId);

        pm.Attempts.Add(new PmAttempt
        {
            At = _timing.CurTime,
            Shooter = args.User,
            Realised = false,
            ErrorDegrees = float.NaN,
        });

        // Выборка скользящая: длинная история только смягчает статистику.
        if (pm.Attempts.Count > 64)
            pm.Attempts.RemoveAt(0);
    }
}
