using Content.Shared._Fish.ClockworkCult;
using Content.Shared._Fish.ClockworkCult.Components;
using Content.Shared._Fish.ClockworkCult.Prototypes;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.ClockworkCult.Systems;

/// <summary>
/// Эффекты писаний Clockwork Cult.
/// </summary>
public sealed partial class FishClockworkScriptureSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedCuffableSystem _cuffs = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private FishClockworkServantSystem _servant = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TurfSystem _turf = default!;

    private static readonly ProtoId<DamageTypePrototype> HeatDamage = "Heat";
    private static readonly ProtoId<DamageTypePrototype> PoisonDamage = "Poison";
    private static readonly EntProtoId ZiptiePrototype = "Zipties";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishClockworkSlabComponent, AfterInteractEvent>(OnSlabAfterInteract);
    }

    public bool CanInvoke(EntityUid user, FishClockworkScripturePrototype proto, bool quiet = false)
    {
        if (!CanPlaceOnePerTile(user, proto, quiet))
            return false;

        return proto.ScriptureId switch
        {
            "ocular_warden" => CanPlaceWarden(user, quiet),
            "vitality_matrix" => CanPlaceVitalityMatrix(user, quiet),
            "abscond" => CanAbscond(user, quiet),
            "clockwork_armaments" => CanArmaments(user, quiet),
            "vanguard" => CanVanguard(user, quiet),
            "spatial_gateway" => CanSpatialGateway(user, quiet),
            _ => true,
        };
    }

    private bool CanPlaceOnePerTile(EntityUid user, FishClockworkScripturePrototype proto, bool quiet)
    {
        if (!proto.OnePerTile || proto.SpawnPrototype == null)
            return true;

        var coords = _transform.GetMoverCoordinates(user);
        if (!_turf.TryGetTileRef(coords, out var userTile))
            return true;

        var spawnId = proto.SpawnPrototype.Value.Id;
        // Радиус покрывает полную диагональ тайла, поэтому сущности на том же тайле всегда попадают в выборку;
        // занятость определяется по гриду и индексам, а не по расстоянию (OnePerTile).
        foreach (var nearby in _lookup.GetEntitiesInRange(coords, 1.5f))
        {
            if (MetaData(nearby).EntityPrototype?.ID != spawnId)
                continue;

            if (!_turf.TryGetTileRef(_transform.GetMoverCoordinates(nearby), out var nearbyTile))
                continue;

            if (nearbyTile.Value.GridUid != userTile.Value.GridUid ||
                nearbyTile.Value.GridIndices != userTile.Value.GridIndices)
                continue;

            if (!quiet)
                _popup.PopupEntity(Loc.GetString("fish-clockwork-one-per-tile"), user, user);
            return false;
        }

        return true;
    }

    public bool TryInvoke(EntityUid slab, EntityUid user, FishClockworkScripturePrototype proto)
    {
        if (!CanInvoke(user, proto))
            return false;

        return proto.ScriptureId switch
        {
            "spawn" => DoSpawn(user, proto),
            "stargazer" => DoSpawn(user, proto),
            "integration_cog" => DoSpawn(user, proto),
            "sigil_transgression" => DoSpawn(user, proto),
            "sigil_submission" => DoSpawn(user, proto),
            "sigil_transmission" => DoSpawn(user, proto),
            "vitality_matrix" => DoSpawn(user, proto),
            "replicant" => DoSpawn(user, proto),
            "wraith_spectacles" => DoSpawn(user, proto),
            "replica_fabricator" => DoSpawn(user, proto),
            "ocular_warden" => DoSpawn(user, proto),
            "judicial_visor" => DoSpawn(user, proto),
            "mania_motor" => DoSpawn(user, proto),
            "clockwork_obelisk" => DoSpawn(user, proto),
            "clockwork_marauder" => DoSpawn(user, proto),
            "kindle" => DoKindle(slab, user),
            "hateful_manacles" => DoManaclesCharge(slab, user),
            "vanguard" => DoVanguard(user),
            "sentinels_compromise" => DoCompromiseCharge(slab, user),
            "abscond" => DoAbscond(user),
            "clockwork_armaments" => DoArmaments(user),
            "spatial_gateway" => DoSpatialGateway(user),
            _ => DoSpawn(user, proto),
        };
    }

    private bool DoSpawn(EntityUid user, FishClockworkScripturePrototype proto)
    {
        if (proto.SpawnPrototype == null)
            return false;

        var coords = _transform.GetMoverCoordinates(user);
        Spawn(proto.SpawnPrototype.Value, coords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Fish/ClockworkCult/magic/clockwork/invoke_general.ogg"), user);
        return true;
    }

    private bool DoKindle(EntityUid slab, EntityUid user)
    {
        var charge = EnsureComp<FishClockworkKindleChargeComponent>(slab);
        charge.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(15);
        Dirty(slab, charge);

        var slabComp = EnsureComp<FishClockworkSlabComponent>(slab);
        slabComp.ActiveChargeScriptureId = "kindle";
        slabComp.ChargeExpiresAt = charge.ExpiresAt;
        Dirty(slab, slabComp);

        _popup.PopupEntity(Loc.GetString("fish-clockwork-kindle-charged"), user, user);
        return true;
    }

    private bool DoManaclesCharge(EntityUid slab, EntityUid user)
    {
        var slabComp = EnsureComp<FishClockworkSlabComponent>(slab);
        slabComp.ActiveChargeScriptureId = "hateful_manacles";
        slabComp.ChargeExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(20);
        Dirty(slab, slabComp);
        _popup.PopupEntity(Loc.GetString("fish-clockwork-manacles-charged"), user, user);
        return true;
    }

    private bool DoCompromiseCharge(EntityUid slab, EntityUid user)
    {
        var slabComp = EnsureComp<FishClockworkSlabComponent>(slab);
        slabComp.ActiveChargeScriptureId = "sentinels_compromise";
        slabComp.ChargeExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(20);
        Dirty(slab, slabComp);
        _popup.PopupEntity(Loc.GetString("fish-clockwork-compromise-charged"), user, user);
        return true;
    }

    private bool CanVanguard(EntityUid user, bool quiet)
    {
        if (HasComp<FishClockworkVanguardComponent>(user))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("fish-clockwork-vanguard-already"), user, user);
            return false;
        }

        return true;
    }

    private bool DoVanguard(EntityUid user)
    {
        var comp = EnsureComp<FishClockworkVanguardComponent>(user);
        comp.EndsAt = _timing.CurTime + TimeSpan.FromSeconds(20);
        comp.AbsorbedStunSeconds = 0f;
        Dirty(user, comp);
        _popup.PopupEntity(Loc.GetString("fish-clockwork-vanguard-active"), user, user);
        return true;
    }

    private bool CanAbscond(EntityUid user, bool quiet)
    {
        // Нельзя Abscond уже на Reebe (рядом с Ark).
        if (TryComp(user, out TransformComponent? xform) &&
            xform.GridUid != null &&
            HasComp<FishClockworkArkComponent>(GetArkOnSameGrid(xform.GridUid.Value)))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("fish-clockwork-abscond-on-reebe"), user, user);
            return false;
        }

        return HasAbscondTarget();
    }

    private EntityUid GetArkOnSameGrid(EntityUid grid)
    {
        var query = EntityQueryEnumerator<FishClockworkArkComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == grid)
                return uid;
        }

        return EntityUid.Invalid;
    }

    private bool HasAbscondTarget()
    {
        var query = EntityQueryEnumerator<FishClockworkReebeMarkerComponent, TransformComponent>();
        while (query.MoveNext(out _, out var marker, out _))
        {
            if (marker.AbscondTarget || marker.ServantSpawn)
                return true;
        }

        return EntityQueryEnumerator<FishClockworkArkComponent>().MoveNext(out _, out _);
    }

    private bool DoAbscond(EntityUid user)
    {
        if (!TryGetAbscondCoords(out var coords))
            return false;

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/blink.ogg"), user);
        _transform.SetCoordinates(user, coords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/blink.ogg"), user);
        _popup.PopupEntity(Loc.GetString("fish-clockwork-abscond-success"), user, user);
        return true;
    }

    private bool TryGetAbscondCoords(out EntityCoordinates coords)
    {
        coords = default;
        var markerQuery = EntityQueryEnumerator<FishClockworkReebeMarkerComponent, TransformComponent>();
        while (markerQuery.MoveNext(out var uid, out var marker, out _))
        {
            if (marker.AbscondTarget || marker.ServantSpawn)
            {
                coords = Transform(uid).Coordinates;
                return true;
            }
        }

        var arkQuery = EntityQueryEnumerator<FishClockworkArkComponent, TransformComponent>();
        while (arkQuery.MoveNext(out var arkUid, out _, out _))
        {
            coords = Transform(arkUid).Coordinates;
            return true;
        }

        return false;
    }

    private bool CanArmaments(EntityUid user, bool quiet)
    {
        if (HasComp<FishClockworkArmamentsComponent>(user))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("fish-clockwork-armaments-already"), user, user);
            return false;
        }

        return true;
    }

    private bool DoArmaments(EntityUid user)
    {
        EnsureComp<FishClockworkArmamentsComponent>(user);
        if (TryComp<FishClockworkServantComponent>(user, out var servant))
        {
            servant.HasArmaments = true;
            Dirty(user, servant);
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Fish/ClockworkCult/magic/clockwork/fellowship_armory.ogg"), user);
        _popup.PopupEntity(Loc.GetString("fish-clockwork-armaments-bound"), user, user);

        // Сразу экипируем броню при первом чтении.
        TryEquipArmaments(user);
        return true;
    }

    public bool TryEquipArmaments(EntityUid user)
    {
        if (!TryComp<FishClockworkArmamentsComponent>(user, out var armaments))
            return false;

        if (TryComp<FishClockworkServantComponent>(user, out var servant) &&
            servant.NextArmorTime != null &&
            servant.NextArmorTime > _timing.CurTime)
        {
            _popup.PopupEntity(Loc.GetString("fish-clockwork-armaments-cooldown"), user, user);
            return false;
        }

        SpawnAndTryEquip(armaments.HelmetPrototype, user, "head");
        SpawnAndTryEquip(armaments.ArmorPrototype, user, "outerClothing");
        SpawnAndTryEquip(armaments.GlovesPrototype, user, "gloves");
        SpawnAndTryEquip(armaments.ShoesPrototype, user, "shoes");

        if (servant != null)
        {
            servant.NextArmorTime = _timing.CurTime + TimeSpan.FromSeconds(FishClockworkCultConstants.ClockworkArmorCooldownSeconds);
            Dirty(user, servant);
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Fish/ClockworkCult/magic/clockwork/fellowship_armory.ogg"), user);
        return true;
    }

    public bool TryCallSpear(EntityUid user)
    {
        if (!TryComp<FishClockworkArmamentsComponent>(user, out var armaments))
            return false;

        if (TryComp<FishClockworkServantComponent>(user, out var servant) &&
            servant.NextSpearTime != null &&
            servant.NextSpearTime > _timing.CurTime)
        {
            _popup.PopupEntity(Loc.GetString("fish-clockwork-spear-cooldown"), user, user);
            return false;
        }

        SpawnAndTryPickup(armaments.SpearPrototype, user);

        if (servant != null)
        {
            servant.NextSpearTime = _timing.CurTime + TimeSpan.FromSeconds(FishClockworkCultConstants.RatvarianSpearCooldownSeconds);
            Dirty(user, servant);
        }

        return true;
    }

    private void SpawnAndTryEquip(EntProtoId proto, EntityUid user, string slot)
    {
        var ent = Spawn(proto, Transform(user).Coordinates);
        // При неудаче экипировки предмет остаётся на полу (fallback).
        _inventory.TryEquip(user, ent, slot, silent: true, force: true);
    }

    private void SpawnAndTryPickup(EntProtoId proto, EntityUid user)
    {
        var ent = Spawn(proto, Transform(user).Coordinates);
        // При неудаче подбора предмет остаётся на полу (fallback).
        _hands.TryPickupAnyHand(user, ent);
    }

    private bool CanSpatialGateway(EntityUid user, bool quiet)
    {
        var others = 0;
        var query = EntityQueryEnumerator<FishClockworkServantComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var mob))
        {
            if (uid == user)
                continue;
            if (_mobState.IsAlive(uid, mob))
                others++;
        }

        var obeliskQuery = EntityQueryEnumerator<FishClockworkObeliskComponent>();
        while (obeliskQuery.MoveNext(out _, out _))
            others++;

        if (others <= 0)
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("fish-clockwork-gateway-no-targets"), user, user);
            return false;
        }

        return true;
    }

    private bool DoSpatialGateway(EntityUid user)
    {
        // Открываем односторонний портал к ближайшему живому слуге или обелиску.
        EntityCoordinates? dest = null;
        var query = EntityQueryEnumerator<FishClockworkServantComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var mob, out var xform))
        {
            if (uid == user || !_mobState.IsAlive(uid, mob))
                continue;
            dest = xform.Coordinates;
            break;
        }

        if (dest == null)
        {
            var ob = EntityQueryEnumerator<FishClockworkObeliskComponent, TransformComponent>();
            while (ob.MoveNext(out _, out _, out var xform))
            {
                dest = xform.Coordinates;
                break;
            }
        }

        if (dest == null)
            return false;

        var rift = Spawn("FishSpatialGatewayPortal", Transform(user).Coordinates);
        var riftComp = EnsureComp<FishCityOfCogsRiftComponent>(rift);
        riftComp.Destination = dest.Value;
        riftComp.IsSpatialGateway = true;
        riftComp.LifetimeSeconds = 4f;
        riftComp.RemainingLifetime = 4f;
        Dirty(rift, riftComp);

        _popup.PopupEntity(Loc.GetString("fish-clockwork-gateway-success"), user, user);
        return true;
    }

    private bool CanPlaceWarden(EntityUid user, bool quiet)
    {
        var origin = Transform(user).Coordinates;
        var query = EntityQueryEnumerator<FishOcularWardenComponent, TransformComponent>();
        while (query.MoveNext(out _, out var warden, out var xform))
        {
            if (xform.Coordinates.TryDistance(EntityManager, origin, out var dist) &&
                dist < warden.ExclusionRange)
            {
                if (!quiet)
                    _popup.PopupEntity(Loc.GetString("fish-clockwork-warden-too-close"), user, user);
                return false;
            }
        }

        return true;
    }

    private bool CanPlaceVitalityMatrix(EntityUid user, bool quiet)
    {
        var origin = Transform(user).Coordinates;
        var query = EntityQueryEnumerator<FishVitalityMatrixComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.Coordinates.TryDistance(EntityManager, origin, out var dist) && dist <= 1.5f)
            {
                if (!quiet)
                    _popup.PopupEntity(Loc.GetString("fish-clockwork-vitality-too-close"), user, user);
                return false;
            }
        }

        return true;
    }

    private void OnSlabAfterInteract(Entity<FishClockworkSlabComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        if (!_servant.IsServant(args.User))
            return;

        if (ent.Comp.ActiveChargeScriptureId == null)
            return;

        if (ent.Comp.ChargeExpiresAt != null && ent.Comp.ChargeExpiresAt < _timing.CurTime)
        {
            ClearCharge(ent);
            return;
        }

        var target = args.Target.Value;
        var handled = ent.Comp.ActiveChargeScriptureId switch
        {
            "kindle" => TryKindleTarget(ent.Owner, args.User, target),
            "hateful_manacles" => TryManacleTarget(args.User, target),
            "sentinels_compromise" => TryCompromiseTarget(args.User, target),
            _ => false,
        };

        if (handled)
        {
            ClearCharge(ent);
            args.Handled = true;
        }
    }

    private void ClearCharge(Entity<FishClockworkSlabComponent> ent)
    {
        ent.Comp.ActiveChargeScriptureId = null;
        ent.Comp.ChargeExpiresAt = null;
        Dirty(ent);
        RemCompDeferred<FishClockworkKindleChargeComponent>(ent.Owner);
    }

    private bool TryKindleTarget(EntityUid slab, EntityUid user, EntityUid target)
    {
        if (_servant.IsServant(target))
            return false;

        if (!TryComp<FishClockworkKindleChargeComponent>(slab, out var charge))
            return false;

        if (!Transform(user).Coordinates.TryDistance(EntityManager, Transform(target).Coordinates, out var dist) ||
            dist > charge.Range)
            return false;

        if (!_stun.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(charge.StunSeconds)))
            return false;

        _popup.PopupEntity(Loc.GetString("fish-clockwork-kindle-hit"), target, PopupType.MediumCaution);
        return true;
    }

    private bool TryManacleTarget(EntityUid user, EntityUid target)
    {
        if (_servant.IsServant(target))
            return false;

        if (!TryComp<CuffableComponent>(target, out var cuffable))
            return false;

        var cuffs = Spawn(ZiptiePrototype, Transform(target).Coordinates);
        if (!TryComp<HandcuffComponent>(cuffs, out _))
        {
            QueueDel(cuffs);
            return false;
        }

        if (!_cuffs.TryAddNewCuffs(target, user, cuffs, cuffable))
        {
            QueueDel(cuffs);
            return false;
        }

        return true;
    }

    private bool TryCompromiseTarget(EntityUid user, EntityUid target)
    {
        if (!_servant.IsServant(target) && target != user)
            return false;

        // Конвертируем brute/burn/airloss в половину toxin через API DamageableSystem (без прямого чтения Damage).
        var heal = new DamageSpecifier();
        heal.DamageDict["Blunt"] = -200;
        heal.DamageDict["Slash"] = -200;
        heal.DamageDict["Piercing"] = -200;
        heal.DamageDict["Heat"] = -200;
        heal.DamageDict["Asphyxiation"] = -200;
        if (!_damageable.TryChangeDamage(target, heal, out var dealt, interruptsDoAfters: false, origin: user))
            return false;

        // Heal даёт отрицательный delta — берём модуль фактически снятого урона.
        var healed = -dealt.GetTotal().Float();
        if (healed <= 0.01f)
            return false;

        var poison = new DamageSpecifier();
        poison.DamageDict[PoisonDamage] = healed * 0.5f;
        _damageable.TryChangeDamage(target, poison, origin: user);

        _popup.PopupEntity(Loc.GetString("fish-clockwork-compromise-done"), target, user);
        return true;
    }
}

