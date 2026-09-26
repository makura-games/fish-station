using Content.Server.Chat.Systems;
using Content.Shared._Fish.JudgeGavel;
using Content.Shared.Chat;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Pinpointer;
using Content.Shared.StatusEffectNew;
using Content.Server.Station.Components;
using Content.Shared.Warps;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server._Fish.JudgeGavel;

/// <summary>
///     System for the Admin Judge Gavel.
/// </summary>
public sealed partial class JudgeGavelSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JudgeGavelComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<JudgeGavelComponent, JudgeGavelDoAfterEvent>(OnDoAfter);
    }

    private void OnUseInHand(Entity<JudgeGavelComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryActivate(ent, args.User))
            args.Handled = true;
    }

    public bool TryActivate(Entity<JudgeGavelComponent> ent, EntityUid user)
    {
        if (!CanActivate(ent, user))
            return false;

        StartActivation(ent, user);
        return true;
    }

    public bool CanActivate(Entity<JudgeGavelComponent> ent, EntityUid user, bool quiet = false)
    {
        // Prevent multiple concurrent swings
        return ent.Comp.ActiveDoAfter == null;
    }

    private void StartActivation(Entity<JudgeGavelComponent> ent, EntityUid user)
    {
        // Force speech
        var chant = Loc.GetString(ent.Comp.Chant);
        _chat.TrySendInGameICMessage(user, chant, InGameICChatType.Speak, ChatTransmitRange.Normal);

        var ev = new JudgeGavelDoAfterEvent();
        var doAfterArgs = new DoAfterArgs(EntityManager, user, ent.Comp.DoAfterTime, ev, ent.Owner)
        {
            BreakOnMove = false,
            BreakOnDamage = true,
            NeedHand = true
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs, out var doAfterId))
        {
            ent.Comp.ActiveDoAfter = doAfterId;
        }
    }

    private void OnDoAfter(Entity<JudgeGavelComponent> ent, ref JudgeGavelDoAfterEvent args)
    {
        ent.Comp.ActiveDoAfter = null;

        if (args.Cancelled || args.Handled)
            return;

        MapCoordinates? targetMapCoords = null;
        var identifier = ent.Comp.CourtroomBeaconId;

        // 1. Destination Discovery: WarpPoint or Beacon lookup
        var warpQuery = EntityQueryEnumerator<WarpPointComponent, TransformComponent>();
        while (warpQuery.MoveNext(out _, out var warp, out var xform))
        {
            if (warp.Location != identifier &&
                identifier != "station-beacon-courtroom")
                continue;
            targetMapCoords = _transform.ToMapCoordinates(xform.Coordinates);
            break;
        }

        // Priority 2: NavMapBeacon
        if (targetMapCoords == null)
        {
            var beaconQuery = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
            while (beaconQuery.MoveNext(out _, out var beacon, out var xform))
            {
                if (beacon.DefaultText != identifier)
                    continue;
                targetMapCoords = _transform.ToMapCoordinates(xform.Coordinates);
                break;
            }
        }

        // 2. Fallback: Grid + Coordinates lookup
        if (targetMapCoords == null)
        {
            EntityUid? centcommGrid = null;
            var stationQuery = EntityQueryEnumerator<BecomesStationComponent>();
            while (stationQuery.MoveNext(out var gridUid, out var becomes))
            {
                if (becomes.Id != "centcomm")
                    continue;
                centcommGrid = gridUid;
                break;
            }

            if (centcommGrid != null)
            {
                var fallbackCoords = new EntityCoordinates(centcommGrid.Value, new System.Numerics.Vector2(28.5f, 36.5f));
                targetMapCoords = _transform.ToMapCoordinates(fallbackCoords);
            }
        }

        if (targetMapCoords == null)
            return;

        var sourceCoords = _transform.GetMapCoordinates(ent.Owner);

        // Play effect at source
        Spawn("RadiationPulse", sourceCoords);

        // Deduplicate entities in range to avoid multiple teleports per entity (prevents gibbing/cloning)
        var processed = new HashSet<EntityUid>();
        var ents = _lookup.GetEntitiesInRange(sourceCoords, ent.Comp.Range);

        foreach (var mob in ents)
        {
            if (!processed.Add(mob))
                continue;

            if (!TryComp<MindContainerComponent>(mob, out var mind) || !mind.HasMind)
                continue;


            // Apply Pacified (using the exact same signature as GenericStatusEffectEntityEffectSystem does for Pax)
            _statusEffects.TryAddStatusEffectDuration(mob, ent.Comp.PacifiedStatusEffect, TimeSpan.FromSeconds(ent.Comp.Duration));
            // Spread out targets within 3 tiles to prevent stacking
            var offset = _random.NextVector2(3.0f);
            var finalTarget = targetMapCoords.Value.Offset(offset);

            // Clear velocity before teleporting to prevent cannonballing into walls/others upon arrival
            if (TryComp<PhysicsComponent>(mob, out var physics))
            {
                _physics.SetLinearVelocity(mob, System.Numerics.Vector2.Zero, body: physics);
                _physics.SetAngularVelocity(mob, 0f, body: physics);
            }

            // Teleport
            _transform.SetMapCoordinates(mob, finalTarget);

            // Effect at individual destination
            Spawn("RadiationPulse", finalTarget);
        }

        args.Handled = true;
    }
}
