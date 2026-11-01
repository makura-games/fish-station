using Content.Server.Atmos.EntitySystems;
using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._Fish.Xenobiology.Extracts;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Database;
using Content.Shared.Explosion.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Temperature.Components;
using Content.Shared.Temperature.Systems;
using Content.Shared.Administration.Logs;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Xenobiology.Extracts;

/// <summary>
/// Реакции экстрактов gold/silver/sepia и отложенные эффекты (oil, orange, dark blue).
/// </summary>
public sealed partial class FishSlimeExtractDeferredReactionSystem : EntitySystem
{
    private const float FreezeRadius = 7f;
    private const float TimestopRadius = 2f;
    private static readonly TimeSpan DelayFiveSeconds = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TimestopDuration = TimeSpan.FromSeconds(14);
    private static readonly TimeSpan EmpDuration = TimeSpan.FromSeconds(7);
    private static readonly FixedPoint2 One = FixedPoint2.New(1);

    private static readonly string[] BorkFood =
    [
        "FoodBreadPlain", "FoodCookie", "FoodMeatCutlet", "FoodTacoShell", "FoodPlateSmall",
    ];

    private static readonly string[] PyritePaints =
    [
        "SprayPainter", "SprayPainterAmmo", "CrayonRainbow",
    ];

    private static readonly string[] BorkDrinks =
    [
        "DrinkGlass", "DrinkWaterBottleFull", "DrinkColaCan", "DrinkSodaWaterCan",
    ];

    private static readonly string[] CeruleanBlueprints =
    [
        "BlueprintFulton", "BlueprintSeismicCharge", "BlueprintDoubleEmergencyTank",
    ];

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private FishSlimeExtractSystem _extracts = default!;
    [Dependency] private EmpSystem _emp = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private TemperatureSystem _temperature = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishSlimeExtractComponent, SolutionChangedEvent>(OnSolutionChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FishSlimeExtractPendingReactionComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pending, out var xform))
        {
            if (_timing.CurTime < pending.TriggerAt)
                continue;

            var coords = _transform.GetMapCoordinates(uid, xform);
            switch (pending.Kind)
            {
                case FishSlimeExtractPendingKind.OilExplosion:
                    TriggerOilExplosion(coords, pending.SourceUser);
                    break;
                case FishSlimeExtractPendingKind.OrangeFire:
                    TriggerOrangeFire(coords);
                    break;
                case FishSlimeExtractPendingKind.DarkBlueFreeze:
                    TriggerDarkBlueFreeze(coords);
                    break;
            }

            RemCompDeferred<FishSlimeExtractPendingReactionComponent>(uid);
        }
    }

    private void OnSolutionChanged(Entity<FishSlimeExtractComponent> ent, ref SolutionChangedEvent args)
    {
        if (_net.IsClient || _timing.ApplyingState || ent.Comp.Uses <= 0)
            return;

        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.SolutionId, out var soln, out var solution))
            return;

        switch (ent.Comp.Color)
        {
            case "gold":
                TryGold(ent, soln, solution);
                break;
            case "silver":
                TrySilver(ent, soln, solution);
                break;
            case "adamantine":
                TryAdamantine(ent, soln, solution);
                break;
            case "bluespace":
                TryBluespace(ent, soln, solution);
                break;
            case "sepia":
                TrySepia(ent, soln, solution);
                break;
            case "pyrite":
                TryPyrite(ent, soln, solution);
                break;
            case "cerulean":
                TryCeruleanBlood(ent, soln, solution);
                break;
            case "black":
                TryBlackBlood(ent, soln, solution);
                break;
            case "oil":
                TryOilPlasma(ent, soln, solution);
                break;
            case "orange":
                TryOrangePlasma(ent, soln, solution);
                break;
            case "darkblue":
                TryDarkBluePlasma(ent, soln, solution);
                break;
            case "yellow":
                TryYellowBlood(ent, soln, solution);
                break;
        }
    }

    private void TryGold(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        if (solution.GetTotalPrototypeQuantity("Plasma") >= One)
        {
            if (!FishSlimeExtractGoldMobCap.TryReserve(5))
            {
                if (ent.Comp.LastInjector is { } user)
                    _popup.PopupEntity(Loc.GetString("fish-slime-extract-gold-cap"), ent, user);
                return;
            }

            if (!TryReactionPlasma(ent, soln, solution, () =>
                {
                    var coords = _transform.GetMapCoordinates(ent);
                    for (var i = 0; i < 5; i++)
                        TagGoldCrit(Spawn(FishSlimeExtractGoldCritPools.PickHostile(_random), coords));
                }))
                FishSlimeExtractGoldMobCap.Release(5);
            else
                return;
        }

        if (_extracts.TryPerformReaction(ent, soln, solution, "Blood", One, () =>
                SpawnGoldCritMob(ent, 3, FishSlimeExtractGoldCritPools.NeutralBlood)))
            return;

        _extracts.TryPerformReaction(ent, soln, solution, "Water", One, () =>
            SpawnGoldCritMob(ent, 1, FishSlimeExtractGoldCritPools.FriendlyWater));
    }

    private void TrySilver(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        if (TryReactionPlasma(ent, soln, solution, () => SpawnBork(ent, BorkFood)))
            return;

        TryReactionWater(ent, soln, solution, () => SpawnBork(ent, BorkDrinks));
    }

    private void SpawnBork(Entity<FishSlimeExtractComponent> ent, string[] pool)
    {
        var count = _random.Next(4, 7);
        for (var i = 0; i < count; i++)
            SpawnNextToOrDrop(_random.Pick(pool), ent);
    }

    private void TryAdamantine(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        _extracts.TryPerformReaction(ent, soln, solution, "Plasma", One, () =>
            SpawnNextToOrDrop("TreasureCoinAdamantine", ent));
    }

    private void TryBluespace(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        if (_extracts.TryPerformReaction(ent, soln, solution, "Blood", One, () =>
            {
                for (var i = 0; i < 25; i++)
                    SpawnNextToOrDrop("FishXenobiologyBluespaceTileFish", ent);
            }))
            return;

        _extracts.TryPerformReaction(ent, soln, solution, "Plasma", One, () =>
            _solutions.TryAddReagent(soln, "Artifexium", One));
    }

    private void TrySepia(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        if (_extracts.TryPerformReaction(ent, soln, solution, "Plasma", One, () => ApplySepiaTimestop(ent)))
            return;

        if (_extracts.TryPerformReaction(ent, soln, solution, "Water", One, () =>
                SpawnNextToOrDrop("HandheldCamera", ent)))
            return;

        _extracts.TryPerformReaction(ent, soln, solution, "Blood", One, () =>
        {
            for (var i = 0; i < 25; i++)
                SpawnNextToOrDrop("FloorTileItemSepia", ent);
        });
    }

    private void ApplySepiaTimestop(Entity<FishSlimeExtractComponent> ent)
    {
        var coords = _transform.GetMapCoordinates(ent);
        foreach (var uid in _lookup.GetEntitiesInRange(coords, TimestopRadius))
        {
            if (ent.Comp.LastInjector == uid)
                continue;

            if (HasComp<MobStateComponent>(uid))
                _stun.TryParalyze(uid, TimestopDuration, true);
        }
    }

    private void TryPyrite(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        _extracts.TryPerformReaction(ent, soln, solution, "Plasma", One, () =>
            SpawnNextToOrDrop(_random.Pick(PyritePaints), ent));
    }

    private void TryCeruleanBlood(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        _extracts.TryPerformReaction(ent, soln, solution, "Blood", One, () =>
            SpawnNextToOrDrop(_random.Pick(CeruleanBlueprints), ent));
    }

    private void SpawnGoldCritMob(Entity<FishSlimeExtractComponent> ent, int count, string proto)
    {
        if (!FishSlimeExtractGoldMobCap.TryReserve(count))
        {
            if (ent.Comp.LastInjector is { } user)
                _popup.PopupEntity(Loc.GetString("fish-slime-extract-gold-cap"), ent, user);
            return;
        }

        var coords = _transform.GetMapCoordinates(ent);
        for (var i = 0; i < count; i++)
            TagGoldCrit(Spawn(proto, coords));
    }

    private void TagGoldCrit(EntityUid mob)
    {
        EnsureComp<FishSlimeExtractGoldCritComponent>(mob).CapWeight = 1;
    }

    private void TryBlackBlood(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        _extracts.TryPerformReaction(ent, soln, solution, "Blood", One, () =>
            _solutions.TryAddReagent(soln, "FishVirusGenesFish", One));
    }

    private void TryOilPlasma(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        if (!_extracts.TryBeginReaction(ent, soln, solution, "Plasma", One))
            return;

        SchedulePending(ent, FishSlimeExtractPendingKind.OilExplosion);
        _popup.PopupEntity(Loc.GetString("fish-slime-extract-react"), ent, ent.Comp.LastInjector);
    }

    private void TryOrangePlasma(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        if (!_extracts.TryBeginReaction(ent, soln, solution, "Plasma", One))
            return;

        SchedulePending(ent, FishSlimeExtractPendingKind.OrangeFire);
        _popup.PopupEntity(Loc.GetString("fish-slime-extract-react"), ent, ent.Comp.LastInjector);
    }

    private void TryDarkBluePlasma(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        if (!_extracts.TryBeginReaction(ent, soln, solution, "Plasma", One))
            return;

        SchedulePending(ent, FishSlimeExtractPendingKind.DarkBlueFreeze);
        _popup.PopupEntity(Loc.GetString("fish-slime-extract-react"), ent, ent.Comp.LastInjector);
    }

    private void TryYellowBlood(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution)
    {
        _extracts.TryPerformReaction(ent, soln, solution, "Blood", One, () =>
        {
            var coords = _transform.GetMapCoordinates(ent);
            _emp.EmpPulse(coords, 7f, 1000f, EmpDuration, ent.Comp.LastInjector);
            _emp.EmpPulse(coords, 3f, 5000f, EmpDuration, ent.Comp.LastInjector);
        });
    }

    private bool TryReactionPlasma(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution, Action apply)
        => _extracts.TryPerformReaction(ent, soln, solution, "Plasma", One, apply);

    private bool TryReactionWater(Entity<FishSlimeExtractComponent> ent, Entity<SolutionComponent> soln, Solution solution, Action apply)
        => _extracts.TryPerformReaction(ent, soln, solution, "Water", One, apply);

    private void SchedulePending(Entity<FishSlimeExtractComponent> ent, FishSlimeExtractPendingKind kind)
    {
        var pending = EnsureComp<FishSlimeExtractPendingReactionComponent>(ent);
        pending.Kind = kind;
        pending.TriggerAt = _timing.CurTime + DelayFiveSeconds;
        pending.SourceUser = ent.Comp.LastInjector;
        Dirty(ent, pending);
    }

    private void TriggerOilExplosion(MapCoordinates coords, EntityUid? user)
    {
        var uid = Spawn(null, coords);
        _explosion.QueueExplosion(uid, "Default", 700f, 3f, 2f, canCreateVacuum: false, user: user);
        _adminLog.Add(LogType.Explosion, LogImpact.High,
            $"Fish oil extract explosion triggered by {ToPrettyString(user)} at {coords}");
        QueueDel(uid);
    }

    private void TriggerOrangeFire(MapCoordinates coords)
    {
        foreach (var uid in _lookup.GetEntitiesInRange(coords, 3f))
        {
            if (TryComp<FlammableComponent>(uid, out var flammable))
                _flammable.Ignite(uid, uid, flammable);
        }
    }

    private void TriggerDarkBlueFreeze(MapCoordinates coords)
    {
        foreach (var uid in _lookup.GetEntitiesInRange(coords, FreezeRadius))
        {
            if (!TryComp<TemperatureComponent>(uid, out var temp))
                continue;

            _temperature.ChangeHeat((uid, temp), -temp.HeatCapacity * 400f);
        }
    }
}
