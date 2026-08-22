using Content.Server.KillTracking;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared._Fish.Achievements;
using Content.Shared._Sunrise.Storyteller;
using Content.Shared.Construction;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Inventory.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;
using Content.Shared.Slippery;
using Content.Shared.Tag;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Achievements;

/// <summary>
/// Event-driven handlers. Один gameplay-event → один EventKey → без duplicate progress.
/// </summary>
public sealed class AchievementConditionSystem : EntitySystem
{
    [Dependency] private readonly AchievementManager _achievements = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly ProtoId<TagPrototype> MouseTag = "Mouse";
    private static readonly ProtoId<TagPrototype> HamsterTag = "Hamster";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);
        SubscribeLocalEvent<SlipEvent>(OnSlip);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<KillReportedEvent>(OnKillReported);
        // Не ActorComponent: RT допускает одну directed-подписку на (comp, event), Actor уже занят StatsBoard.
        SubscribeLocalEvent<AchievementTrackedComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<AchievementTrackedComponent, ItemConstructionCreated>(OnCrafted);
        SubscribeLocalEvent<AchievementTrackedComponent, DidEquipEvent>(OnEquipped);
        SubscribeLocalEvent<AchievementTrackedComponent, UserInteractHandEvent>(OnUserInteractHand);
        SubscribeLocalEvent<GameRuleStartedEvent>(OnGameRuleStarted);
        // Broadcast: directed EmergencyShuttleComponent+FTLCompleted уже занят EmergencyShuttleSystem.
        SubscribeLocalEvent<FTLCompletedEvent>(OnEmergencyShuttleArrived);
        SubscribeLocalEvent<SunriseExplosionEvent>(OnExplosion);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _achievements.OnRoundStarting();
    }

    private async void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        EnsureComp<AchievementTrackedComponent>(ev.Mob);
        _achievements.MarkRoundPresence(ev.Player);

        if (ev.LateJoin)
        {
            await _achievements.ContributeAsync(
                ev.Player,
                AchievementConditionKeys.FirstLateJoin,
                new AchievementTriggerContext(EventKey: $"latejoin:{ev.Player.UserId}:{ev.JoinOrder}"));
        }

        if (!string.IsNullOrEmpty(ev.JobId))
        {
            await _achievements.ContributeAsync(
                ev.Player,
                AchievementConditionKeys.JobPlay,
                new AchievementTriggerContext(
                    JobId: ev.JobId,
                    EventKey: $"job:{ev.Player.UserId}:{ev.JobId}"));
        }
    }

    private async void OnRoundEnd(RoundEndMessageEvent ev)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } ent)
                continue;

            if (!TryComp<MobStateComponent>(ent, out var mob) || mob.CurrentState != MobState.Alive)
                continue;

            var onShuttle = IsOnEmergencyShuttle(ent);
            // Без EventKey → once-per-round на ачивку (один тик выживания за раунд).
            var ctx = new AchievementTriggerContext(
                OnEmergencyShuttle: onShuttle,
                RequireInRound: false); // уже PostRound

            await _achievements.ContributeAsync(session, AchievementConditionKeys.RoundEndAlive, ctx);
            await _achievements.ContributeAsync(session, AchievementConditionKeys.RoundSurvive, ctx);
            await _achievements.ContributeAsync(
                session,
                AchievementConditionKeys.Counter,
                new AchievementTriggerContext(
                    CounterKey: "rounds-survived",
                    OnEmergencyShuttle: onShuttle,
                    RequireInRound: false));

            // ShuttleArrive только на FTLCompleted — иначе +2 за раунд (FTL + round-end).
            if (_mind.TryGetMind(ent, out var mindId, out _) && _roles.MindIsAntagonist(mindId))
                await _achievements.ContributeAsync(session, AchievementConditionKeys.AntagWin, ctx);
        }
    }

    private bool IsOnEmergencyShuttle(EntityUid ent)
    {
        var grid = Transform(ent).GridUid;
        return grid != null && HasComp<EmergencyShuttleComponent>(grid.Value);
    }

    private void OnSlip(ref SlipEvent ev)
    {
        EnsureComp<AchievementSlippedMarkerComponent>(ev.Slipped);
    }

    private async void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (!_mind.TryGetMind(args.Target, out _, out var mindComp) || mindComp.UserId is not { } userId)
            return;

        if (!_players.TryGetSessionById(userId, out var session))
            return;

        var suicide = args.Origin == args.Target;
        var deathKey = $"death:{GetNetEntity(args.Target)}";

        await _achievements.ContributeAsync(
            session,
            AchievementConditionKeys.Death,
            new AchievementTriggerContext(IsSuicide: suicide, EventKey: deathKey));

        if (HasComp<AchievementSlippedMarkerComponent>(args.Target))
        {
            await _achievements.ContributeAsync(
                session,
                AchievementConditionKeys.SlipDeath,
                new AchievementTriggerContext(IsSuicide: suicide, EventKey: $"slipdeath:{GetNetEntity(args.Target)}"));
        }

        RemComp<AchievementSlippedMarkerComponent>(args.Target);
    }

    private void OnKillReported(ref KillReportedEvent ev)
    {
        if (ev.Suicide || ev.Primary is not KillPlayerSource playerKill)
            return;

        if (!_players.TryGetSessionById(playerKill.PlayerId, out var session))
            return;

        if (_tags.HasTag(ev.Entity, MouseTag) || _tags.HasTag(ev.Entity, HamsterTag))
            return;

        // Godmode / admin-инструменты на киллере — не фарм.
        if (session.AttachedEntity is { } killerEnt && HasComp<GodmodeComponent>(killerEnt))
            return;

        var victimIsPlayerHumanoid = HasComp<ActorComponent>(ev.Entity) &&
                                     HasComp<HumanoidProfileComponent>(ev.Entity);

        // Один kill-event на жертву; разные жертвы → разный EventKey → можно несколько за раунд.
        _ = _achievements.ContributeAsync(
            session,
            AchievementConditionKeys.Kill,
            new AchievementTriggerContext(
                VictimIsPlayerHumanoid: victimIsPlayerHumanoid,
                EventKey: $"kill:{GetNetEntity(ev.Entity)}"));
    }

    private async void OnDamageChanged(EntityUid uid, AchievementTrackedComponent tracked, DamageChangedEvent args)
    {
        if (args.DamageDelta == null)
            return;

        if (!TryComp<ActorComponent>(uid, out _))
            return;

        // Heal: только чужой лекарь без godmode; EventKey на пациента+секундный bucket.
        if (!args.DamageIncreased && args.Origin is { } healer && healer != uid)
        {
            if (HasComp<GodmodeComponent>(healer))
                return;

            if (TryComp<ActorComponent>(healer, out var healerActor))
            {
                var bucket = (int)_timing.CurTime.TotalSeconds;
                await _achievements.ContributeAsync(
                    healerActor.PlayerSession,
                    AchievementConditionKeys.Heal,
                    new AchievementTriggerContext(EventKey: $"heal:{GetNetEntity(uid)}:{bucket}"));
            }
        }

        // Damage dealt: не дублируем kill (KillReported). Грубый bucket + жертва.
        if (args.DamageIncreased && args.Origin is { } attacker && attacker != uid)
        {
            if (HasComp<GodmodeComponent>(attacker))
                return;

            if (!TryComp<ActorComponent>(attacker, out var attackerActor))
                return;

            var bucket = (int)_timing.CurTime.TotalSeconds;
            await _achievements.ContributeAsync(
                attackerActor.PlayerSession,
                AchievementConditionKeys.DamageDealt,
                new AchievementTriggerContext(
                    VictimIsPlayerHumanoid: HasComp<HumanoidProfileComponent>(uid),
                    EventKey: $"dmg:{GetNetEntity(uid)}:{GetNetEntity(attacker)}:{bucket}"));
        }
    }

    private void OnCrafted(EntityUid uid, AchievementTrackedComponent tracked, ref ItemConstructionCreated args)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        _ = _achievements.ContributeAsync(
            actor.PlayerSession,
            AchievementConditionKeys.Craft,
            new AchievementTriggerContext(EventKey: $"craft:{GetNetEntity(args.Item)}"));
    }

    private async void OnEquipped(EntityUid uid, AchievementTrackedComponent tracked, DidEquipEvent args)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        await _achievements.ContributeAsync(
            actor.PlayerSession,
            AchievementConditionKeys.ItemPickup,
            new AchievementTriggerContext(EventKey: $"equip:{GetNetEntity(args.Equipment)}"));
    }

    private async void OnUserInteractHand(EntityUid uid, AchievementTrackedComponent tracked, UserInteractHandEvent args)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        var bucket = _timing.CurTick.Value / 10;
        await _achievements.ContributeAsync(
            actor.PlayerSession,
            AchievementConditionKeys.Interaction,
            new AchievementTriggerContext(EventKey: $"interact:{GetNetEntity(args.Target)}:{bucket}"));
    }

    private void OnGameRuleStarted(ref GameRuleStartedEvent ev)
    {
        if (string.IsNullOrEmpty(ev.RuleId))
            return;

        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity == null)
                continue;

            _ = _achievements.ContributeAsync(
                session,
                AchievementConditionKeys.StationEvent,
                new AchievementTriggerContext(
                    EventId: ev.RuleId,
                    EventKey: $"event:{ev.RuleId}:{session.UserId}"));
        }
    }

    private void OnEmergencyShuttleArrived(ref FTLCompletedEvent args)
    {
        var uid = args.Entity;
        if (!HasComp<EmergencyShuttleComponent>(uid))
            return;

        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } ent)
                continue;

            if (Transform(ent).GridUid != uid)
                continue;

            if (!TryComp<MobStateComponent>(ent, out var mob) || mob.CurrentState != MobState.Alive)
                continue;

            _ = _achievements.ContributeAsync(
                session,
                AchievementConditionKeys.ShuttleArrive,
                new AchievementTriggerContext(
                    OnEmergencyShuttle: true,
                    EventKey: $"shuttle:{session.UserId}:{GetNetEntity(uid)}"));
        }
    }

    private async void OnExplosion(SunriseExplosionEvent ev)
    {
        var radius = Math.Max(ev.AffectedTiles / 4f, 8f);
        var epicenterKey = $"{ev.Epicenter.MapId}:{ev.Epicenter.Position.X:F0}:{ev.Epicenter.Position.Y:F0}";
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } ent)
                continue;

            var coords = _transform.GetMapCoordinates(ent);
            if (coords.MapId != ev.Epicenter.MapId)
                continue;

            if ((coords.Position - ev.Epicenter.Position).Length() > radius)
                continue;

            await _achievements.ContributeAsync(
                session,
                AchievementConditionKeys.Explosion,
                new AchievementTriggerContext(EventKey: $"boom:{epicenterKey}:{session.UserId}"));
        }
    }
}
