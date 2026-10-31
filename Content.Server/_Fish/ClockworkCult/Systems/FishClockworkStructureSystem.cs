using Content.Server.Chat.Systems;
using Content.Shared._Fish.ClockworkCult;
using Content.Shared._Fish.ClockworkCult.Components;
using Content.Shared._Fish.ClockworkCult.Events;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Fish.ClockworkCult.Systems;

/// <summary>
/// Структуры: Ocular Warden, Mania Motor, Stargazer, Herald Beacon, Obelisk, Eminence Spire.
/// </summary>
public sealed partial class FishClockworkStructureSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private FishClockworkArkSystem _ark = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private FishClockworkPowerSystem _power = default!;
    [Dependency] private FishClockworkServantSystem _servant = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMapSystem _maps = default!;

    private static readonly ProtoId<DamageTypePrototype> HeatDamage = "Heat";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishHeraldBeaconComponent, ActivateInWorldEvent>(OnHeraldActivate);
        SubscribeLocalEvent<FishEminenceSpireComponent, ActivateInWorldEvent>(OnEminenceActivate);
        SubscribeLocalEvent<FishClockworkObeliskComponent, ActivateInWorldEvent>(OnObeliskActivate);
        SubscribeLocalEvent<FishClockworkObeliskComponent, GetVerbsEvent<AlternativeVerb>>(OnObeliskVerbs);
        SubscribeLocalEvent<FishHeraldBeaconComponent, GetVerbsEvent<AlternativeVerb>>(OnHeraldVerbs);
        SubscribeLocalEvent<FishEminenceComponent, FishEminenceMassRecallActionEvent>(OnEminenceMassRecall);
        SubscribeLocalEvent<FishEminenceComponent, ComponentShutdown>(OnEminenceShutdown);
        SubscribeLocalEvent<FishJudicialVisorComponent, AfterInteractEvent>(OnJudicialVisor);
        SubscribeLocalEvent<FishRatvarianSpearComponent, MeleeHitEvent>(OnSpearHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateWardens(frameTime);
        UpdateManiaMotors(frameTime);
        UpdateStargazers(frameTime);
        UpdateHeraldVotes();
    }

    private void UpdateWardens(float frameTime)
    {
        var query = EntityQueryEnumerator<FishOcularWardenComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var warden, out var xform))
        {
            warden.Accumulator += frameTime;
            if (warden.Accumulator < warden.TickInterval)
                continue;

            warden.Accumulator = 0f;

            foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, warden.Range))
            {
                if (_servant.IsServant(target.Owner) || !_mobState.IsAlive(target.Owner))
                    continue;

                var dmg = new DamageSpecifier();
                dmg.DamageDict[HeatDamage] = warden.DamagePerTick;
                _damageable.TryChangeDamage(target.Owner, dmg, origin: uid);
                _audio.PlayPvs(warden.AttackSound, uid);
                break; // одна цель за тик
            }
        }
    }

    private void UpdateManiaMotors(float frameTime)
    {
        var query = EntityQueryEnumerator<FishManiaMotorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var motor, out var xform))
        {
            motor.Accumulator += frameTime;
            if (motor.Accumulator < motor.TickInterval)
                continue;

            motor.Accumulator = 0f;

            var tickCost = motor.PowerDrainPerSecond * motor.TickInterval;

            // Авто-включение после восстановления энергии.
            if (!motor.Enabled)
            {
                if (!_power.HasPower(tickCost))
                    continue;

                motor.Enabled = true;
                Dirty(uid, motor);
            }

            if (!_power.TrySpendPower(tickCost, quiet: true))
            {
                motor.Enabled = false;
                Dirty(uid, motor);
                continue;
            }

            foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, motor.Range))
            {
                if (_servant.IsServant(target.Owner))
                    continue;

                if (!_mobState.IsAlive(target.Owner))
                    continue;

                var dmg = new DamageSpecifier();
                dmg.DamageDict[HeatDamage] = motor.DamagePerTick;
                _damageable.TryChangeDamage(target.Owner, dmg, origin: uid);
                _stun.TryUpdateParalyzeDuration(target.Owner, TimeSpan.FromSeconds(0.5));
            }
        }
    }

    private void UpdateStargazers(float frameTime)
    {
        var query = EntityQueryEnumerator<FishStargazerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var gazer, out var xform))
        {
            gazer.Accumulator += frameTime;
            if (gazer.Accumulator < 1f)
                continue;

            // Fish-edit: сначала accumulator, потом дорогая проверка starlight.
            if (!HasStarlight(xform))
            {
                gazer.Accumulator = 0f;
                continue;
            }

            gazer.Accumulator = 0f;
            _power.TryAdjustPower(gazer.PowerPerSecond);
        }
    }

    private bool HasStarlight(TransformComponent xform)
    {
        // Упрощение tg «starlight»: нет грида или соседний тайл пуст (край / космос).
        if (xform.GridUid == null)
            return true;

        if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
            return true;

        var indices = _maps.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
        // Fish-start: проверяем 4 соседа — собственный тайл структуры почти всегда заполнен.
        foreach (var offset in new[] { new Vector2i(1, 0), new Vector2i(-1, 0), new Vector2i(0, 1), new Vector2i(0, -1) })
        {
            var neighbor = _maps.GetTileRef(xform.GridUid.Value, grid, indices + offset);
            if (neighbor.Tile.IsEmpty)
                return true;
        }
        // Fish-end

        return false;
    }

    private void OnHeraldActivate(Entity<FishHeraldBeaconComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (!_servant.IsServant(args.User))
            return;

        if (ent.Comp.Activated)
        {
            _popup.PopupEntity(Loc.GetString("fish-clockwork-herald-already"), args.User, args.User);
            args.Handled = true;
            return;
        }

        if (ent.Comp.VoteEndsAt == null)
        {
            ent.Comp.VoteEndsAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.VoteDurationSeconds);
            ent.Comp.VotesYes.Clear();
            ent.Comp.VotesNo.Clear();
            _power.HierophantAnnounce(Loc.GetString("fish-clockwork-herald-vote-start"));
        }

        ent.Comp.VotesNo.Remove(args.User);
        ent.Comp.VotesYes.Add(args.User);
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("fish-clockwork-herald-voted-yes"), args.User, args.User);
        args.Handled = true;
    }

    private void OnHeraldVerbs(Entity<FishHeraldBeaconComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_servant.IsServant(args.User))
            return;

        if (ent.Comp.Activated || ent.Comp.VoteEndsAt == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fish-clockwork-herald-verb-vote-no"),
            Act = () =>
            {
                if (ent.Comp.Activated || ent.Comp.VoteEndsAt == null)
                    return;

                ent.Comp.VotesYes.Remove(user);
                ent.Comp.VotesNo.Add(user);
                Dirty(ent);
                _popup.PopupEntity(Loc.GetString("fish-clockwork-herald-voted-no"), user, user);
            },
            Priority = 1,
        });
    }

    private void UpdateHeraldVotes()
    {
        var query = EntityQueryEnumerator<FishHeraldBeaconComponent>();
        while (query.MoveNext(out var uid, out var beacon))
        {
            if (beacon.Activated || beacon.VoteEndsAt == null)
                continue;

            if (_timing.CurTime < beacon.VoteEndsAt)
                continue;

            if (beacon.VotesYes.Count >= beacon.VotesNo.Count && beacon.VotesYes.Count > 0)
            {
                beacon.Activated = true;
                _power.SetHeralded(true);
                _chat.DispatchGlobalAnnouncement(
                    Loc.GetString("fish-clockwork-herald-declared"),
                    Loc.GetString("fish-clockwork-announcer"),
                    playDefault: true,
                    colorOverride: Color.FromHex("#BE8700"));
                _power.HierophantAnnounce(Loc.GetString("fish-clockwork-herald-buff"));
            }
            else
            {
                _power.HierophantAnnounce(Loc.GetString("fish-clockwork-herald-rejected"));
            }

            beacon.VoteEndsAt = null;
            Dirty(uid, beacon);
        }
    }

    private void OnEminenceActivate(Entity<FishEminenceSpireComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || !_servant.IsServant(args.User))
            return;

        if (ent.Comp.CurrentEminence != null && Exists(ent.Comp.CurrentEminence.Value))
        {
            _popup.PopupEntity(Loc.GetString("fish-clockwork-eminence-exists"), args.User, args.User);
            args.Handled = true;
            return;
        }

        ent.Comp.CurrentEminence = args.User;
        var eminence = EnsureComp<FishEminenceComponent>(args.User);
        ApplyEminenceForm(args.User, eminence);
        Dirty(ent);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Fish/ClockworkCult/machines/clockcult/eminence_selected.ogg"), ent.Owner);
        _power.HierophantAnnounce(Loc.GetString("fish-clockwork-eminence-selected", ("name", Name(args.User))));
        args.Handled = true;
    }

    private void ApplyEminenceForm(EntityUid user, FishEminenceComponent eminence)
    {
        // Упрощённая «бестелесность»: отключаем hard-коллизию.
        if (TryComp<FixturesComponent>(user, out var fixtures))
        {
            foreach (var (_, fixture) in fixtures.Fixtures)
                _physics.SetHard(user, fixture, false, manager: fixtures);
        }

        if (eminence.MassRecallActionEntity == null)
            _actions.AddAction(user, ref eminence.MassRecallActionEntity, eminence.MassRecallAction);

        Dirty(user, eminence);
        _popup.PopupEntity(Loc.GetString("fish-clockwork-eminence-ascended"), user, user);
    }

    private void OnEminenceMassRecall(Entity<FishEminenceComponent> ent, ref FishEminenceMassRecallActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_ark.TryMassRecall())
        {
            _popup.PopupEntity(Loc.GetString("fish-clockwork-eminence-no-recall"), ent.Owner, ent.Owner);
            return;
        }

        args.Handled = true;
    }

    private void OnEminenceShutdown(Entity<FishEminenceComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Comp.MassRecallActionEntity);
    }

    private void OnObeliskActivate(Entity<FishClockworkObeliskComponent> ent, ref ActivateInWorldEvent args)
    {
        // По умолчанию — Hierophant broadcast; gateway через Alternative verb.
        if (args.Handled || !args.Complex || !_servant.IsServant(args.User))
            return;

        TryObeliskBroadcast(ent, args.User);
        args.Handled = true;
    }

    private void OnObeliskVerbs(Entity<FishClockworkObeliskComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_servant.IsServant(args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fish-clockwork-obelisk-verb-broadcast"),
            Act = () => TryObeliskBroadcast(ent, user),
            Priority = 2,
        });

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fish-clockwork-obelisk-verb-gateway"),
            Act = () => TryObeliskGateway(ent, user),
            Priority = 1,
        });
    }

    private void TryObeliskBroadcast(Entity<FishClockworkObeliskComponent> ent, EntityUid user)
    {
        if (!_power.TrySpendPower(ent.Comp.BroadcastPowerCost))
        {
            _popup.PopupEntity(Loc.GetString("fish-clockwork-not-enough-power"), user, user);
            return;
        }

        _power.HierophantAnnounce(Loc.GetString("fish-clockwork-obelisk-broadcast", ("name", Name(user))));
        _popup.PopupEntity(Loc.GetString("fish-clockwork-obelisk-broadcast-local"), user, user);
    }

    private void TryObeliskGateway(Entity<FishClockworkObeliskComponent> ent, EntityUid user)
    {
        if (ent.Comp.GatewayUsesRemaining <= 0)
        {
            _popup.PopupEntity(Loc.GetString("fish-clockwork-obelisk-gateway-empty"), user, user);
            return;
        }

        // Fish-start: сначала цель, потом списание — без refund/эксплойта HeraldCostMultiplier.
        EntityCoordinates? dest = null;
        var query = EntityQueryEnumerator<FishClockworkServantComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var servant, out _, out var mob, out var xform))
        {
            if (servant == user || !_mobState.IsAlive(servant, mob))
                continue;
            dest = xform.Coordinates;
            break;
        }

        if (dest == null)
        {
            var markerQuery = EntityQueryEnumerator<FishClockworkReebeMarkerComponent, TransformComponent>();
            while (markerQuery.MoveNext(out var markerUid, out var marker, out _))
            {
                if (marker.CrewInvasion || marker.AbscondTarget)
                {
                    dest = Transform(markerUid).Coordinates;
                    break;
                }
            }
        }

        if (dest == null)
        {
            _popup.PopupEntity(Loc.GetString("fish-clockwork-gateway-no-targets"), user, user);
            return;
        }

        if (!_power.TrySpendPower(ent.Comp.GatewayPowerCost))
        {
            _popup.PopupEntity(Loc.GetString("fish-clockwork-not-enough-power"), user, user);
            return;
        }
        // Fish-end

        var portal = Spawn("FishSpatialGatewayPortal", Transform(ent).Coordinates);
        var rift = EnsureComp<FishCityOfCogsRiftComponent>(portal);
        rift.Destination = dest;
        rift.IsSpatialGateway = true;
        rift.LifetimeSeconds = ent.Comp.GatewayLifetimeSeconds;
        rift.RemainingLifetime = ent.Comp.GatewayLifetimeSeconds;
        Dirty(portal, rift);

        var despawn = EnsureComp<TimedDespawnComponent>(portal);
        despawn.Lifetime = ent.Comp.GatewayLifetimeSeconds;

        ent.Comp.GatewayUsesRemaining--;
        Dirty(ent);
        _popup.PopupEntity(
            Loc.GetString("fish-clockwork-obelisk-gateway-open", ("uses", ent.Comp.GatewayUsesRemaining)),
            user,
            user);
    }

    private void OnJudicialVisor(Entity<FishJudicialVisorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !_servant.IsServant(args.User))
            return;

        if (ent.Comp.NextUseTime != null && ent.Comp.NextUseTime > _timing.CurTime)
        {
            _popup.PopupEntity(Loc.GetString("fish-clockwork-judicial-cooldown"), args.User, args.User);
            return;
        }

        var coords = args.ClickLocation;
        var user = args.User;
        var radius = ent.Comp.SmiteRadius;
        ent.Comp.NextUseTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.CooldownSeconds);
        Dirty(ent);

        Timer.Spawn(TimeSpan.FromSeconds(ent.Comp.SmiteDelaySeconds), () =>
        {
            if (!Exists(ent.Owner))
                return;

            foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(coords, radius))
            {
                if (_servant.IsServant(target.Owner))
                    continue;

                _stun.TryUpdateParalyzeDuration(target.Owner, TimeSpan.FromSeconds(2));
                var dmg = new DamageSpecifier();
                dmg.DamageDict[HeatDamage] = 15f;
                _damageable.TryChangeDamage(target.Owner, dmg, origin: user);
            }
        });

        _popup.PopupEntity(Loc.GetString("fish-clockwork-judicial-smite"), args.User, args.User);
        args.Handled = true;
    }

    private void OnSpearHit(Entity<FishRatvarianSpearComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (var hit in args.HitEntities)
        {
            // Fish-edit: vitality только за живых мобов, не за стены/трупы.
            if (_servant.IsServant(hit) ||
                !TryComp<MobStateComponent>(hit, out var mob) ||
                !_mobState.IsAlive(hit, mob))
                continue;
            _power.AdjustVitality(ent.Comp.VitalityPerHit);
        }
    }
}

