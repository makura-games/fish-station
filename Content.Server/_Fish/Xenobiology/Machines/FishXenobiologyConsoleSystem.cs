using Content.Shared._Fish.Xenobiology.Extracts;
using Content.Shared._Fish.Xenobiology.Machines;
using Content.Shared._Fish.Xenobiology.Slimes;
using Content.Shared.Access.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Speech.Components;
using Content.Shared.UserInterface;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._Fish.Xenobiology.Machines;

/// <summary>
/// Консоль ксенобиологии без камерного глаза: радиус вокруг машины, хранение до 6 слаймов.
/// </summary>
public sealed partial class FishXenobiologyConsoleSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FishSlimePotionSystem _potions = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ISharedPlayerManager _players = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishXenobiologyConsoleComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FishXenobiologyConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<FishXenobiologyConsoleComponent, InteractUsingEvent>(OnInteractUsing);

        SubscribeLocalEvent<FishXenobiologyConsoleComponent, FishXenoConsoleReleaseAllMessage>(OnReleaseAll);
        SubscribeLocalEvent<FishXenobiologyConsoleComponent, FishXenoConsoleCollectSlimesMessage>(OnCollectSlimes);
        SubscribeLocalEvent<FishXenobiologyConsoleComponent, FishXenoConsoleEjectMonkeyMessage>(OnEjectMonkey);
        SubscribeLocalEvent<FishXenobiologyConsoleComponent, FishXenoConsoleScanStoredMessage>(OnScanStored);
        SubscribeLocalEvent<FishXenobiologyConsoleComponent, FishXenoConsoleFeedPotionMessage>(OnFeedPotion);
        SubscribeLocalEvent<FishXenobiologyConsoleComponent, FishXenoConsoleRecycleMonkeysMessage>(OnRecycleMonkeys);

        InitializeVerbs();
    }

    partial void InitializeVerbs();

    private void OnShutdown(Entity<FishXenobiologyConsoleComponent> ent, ref ComponentShutdown args)
    {
        ReleaseAllSlimes(ent, null);
    }

    private void OnUiOpened(Entity<FishXenobiologyConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        DirtyUi(ent);
    }

    private void OnInteractUsing(Entity<FishXenobiologyConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<FishSlimePotionComponent>(args.Used, out var potionComp))
        {
            args.Handled = true;
            ent.Comp.LoadedPotionKind = potionComp.Kind;
            Dirty(ent);
            QueueDel(args.Used);
            _popup.PopupEntity(Loc.GetString("fish-xeno-console-potion-loaded"), ent, args.User);
            DirtyUi(ent);
            return;
        }

        if (MetaData(args.Used).EntityPrototype?.ID is { } id &&
            (id == "FishXenobiologyMonkeyCube" || id == "MonkeyCube"))
        {
            args.Handled = true;
            ent.Comp.MonkeyCubeCount++;
            Dirty(ent);
            QueueDel(args.Used);
            _popup.PopupEntity(Loc.GetString("fish-xeno-console-cube-added"), ent, args.User);
            DirtyUi(ent);
        }
    }

    private void OnReleaseAll(Entity<FishXenobiologyConsoleComponent> ent, ref FishXenoConsoleReleaseAllMessage args)
    {
        if (!CanOperate(ent, args.Actor))
            return;

        ReleaseAllSlimes(ent, args.Actor);
        DirtyUi(ent, Loc.GetString("fish-xeno-console-released"));
    }

    private void OnCollectSlimes(Entity<FishXenobiologyConsoleComponent> ent, ref FishXenoConsoleCollectSlimesMessage args)
    {
        if (!CanOperate(ent, args.Actor))
            return;

        var coords = _transform.GetMapCoordinates(ent);
        var collected = 0;

        foreach (var uid in _lookup.GetEntitiesInRange<FishSlimeComponent>(coords, ent.Comp.Radius))
        {
            if (ent.Comp.StoredSlimes.Count >= FishXenobiologyConsoleComponent.MaxStoredSlimes)
                break;

            if (ent.Comp.StoredSlimes.Contains(uid))
                continue;

            if (!TryStoreSlime(ent, uid))
                continue;

            collected++;
        }

        DirtyUi(ent, Loc.GetString("fish-xeno-console-collected", ("count", collected)));
    }

    private void OnEjectMonkey(Entity<FishXenobiologyConsoleComponent> ent, ref FishXenoConsoleEjectMonkeyMessage args)
    {
        if (!CanOperate(ent, args.Actor))
            return;

        if (ent.Comp.MonkeyCubeCount <= 0)
        {
            DirtyUi(ent, Loc.GetString("fish-xeno-console-no-cubes"));
            return;
        }

        ent.Comp.MonkeyCubeCount--;
        Dirty(ent);
        Spawn("MobMonkey", _transform.GetMapCoordinates(ent));
        DirtyUi(ent, Loc.GetString("fish-xeno-console-monkey-out"));
    }

    private void OnScanStored(Entity<FishXenobiologyConsoleComponent> ent, ref FishXenoConsoleScanStoredMessage args)
    {
        if (!CanOperate(ent, args.Actor))
            return;

        if (ent.Comp.StoredSlimes.Count == 0)
        {
            DirtyUi(ent, Loc.GetString("fish-xeno-console-no-slimes"));
            return;
        }

        foreach (var slime in ent.Comp.StoredSlimes.ToArray())
        {
            if (!TryComp<FishSlimeComponent>(slime, out var comp))
                continue;

            _popup.PopupEntity(Loc.GetString("fish-slime-examine",
                ("nutrition", (int) comp.Nutrition),
                ("max", (int) comp.MaxNutrition),
                ("grown", comp.AmountGrown),
                ("need", FishSlimeMutation.EvolutionThreshold),
                ("mutation", comp.MutationChance),
                ("cores", comp.Cores),
                ("power", comp.PowerLevel),
                ("adult", comp.IsAdult),
                ("rabid", comp.Rabid),
                ("docile", comp.Docile),
                ("trained", comp.Trained)), slime, args.Actor);
        }

        DirtyUi(ent, Loc.GetString("fish-xeno-console-scanned"));
    }

    private void OnFeedPotion(Entity<FishXenobiologyConsoleComponent> ent, ref FishXenoConsoleFeedPotionMessage args)
    {
        if (!CanOperate(ent, args.Actor))
            return;

        if (ent.Comp.LoadedPotionKind is not { } kind)
        {
            DirtyUi(ent, Loc.GetString("fish-xeno-console-no-potion"));
            return;
        }

        foreach (var slime in ent.Comp.StoredSlimes.ToArray())
        {
            if (!TryComp<FishSlimeComponent>(slime, out _))
                continue;

            if (_potions.TryApplyFromConsole(slime, kind, args.Actor))
            {
                ent.Comp.LoadedPotionKind = null;
                Dirty(ent);
                DirtyUi(ent, Loc.GetString("fish-xeno-console-fed"));
                return;
            }
        }

        DirtyUi(ent, Loc.GetString("fish-xeno-console-feed-fail"));
    }

    private void OnRecycleMonkeys(Entity<FishXenobiologyConsoleComponent> ent, ref FishXenoConsoleRecycleMonkeysMessage args)
    {
        if (!CanOperate(ent, args.Actor))
            return;

        var coords = _transform.GetMapCoordinates(ent);
        var recycled = 0;

        var grind = GetRecycleGrind(ent);

        foreach (var uid in _lookup.GetEntitiesInRange(coords, ent.Comp.Radius))
        {
            if (!HasComp<MonkeyAccentComponent>(uid) || _mobState.IsAlive(uid))
                continue;

            QueueDel(uid);
            ent.Comp.RecycleProgress++;
            recycled++;

            while (ent.Comp.RecycleProgress >= grind)
            {
                ent.Comp.RecycleProgress -= grind;
                ent.Comp.MonkeyCubeCount++;
            }
        }

        Dirty(ent);
        DirtyUi(ent, Loc.GetString("fish-xeno-console-recycled", ("count", recycled)));
    }

    private int GetRecycleGrind(Entity<FishXenobiologyConsoleComponent> console)
    {
        if (console.Comp.LinkedRecycler is { } rec && TryComp<FishMonkeyRecyclerComponent>(rec, out var rc))
            return Math.Max(1, rc.RequiredGrind);

        return FishXenobiologyConsoleComponent.RecycleGrindRequired;
    }

    private bool CanOperate(Entity<FishXenobiologyConsoleComponent> ent, EntityUid user)
    {
        if (!_access.IsAllowed(user, ent))
        {
            _popup.PopupEntity(Loc.GetString("fish-xeno-console-access"), ent, user);
            return false;
        }

        if (TryComp<ApcPowerReceiverComponent>(ent, out var power) && !power.Powered)
        {
            _popup.PopupEntity(Loc.GetString("fish-xeno-console-unpowered"), ent, user);
            return false;
        }

        return true;
    }

    private bool TryStoreSlime(Entity<FishXenobiologyConsoleComponent> console, EntityUid slime)
    {
        if (!TryComp<FishSlimeComponent>(slime, out var comp))
            return false;

        if (!_mobState.IsAlive(slime))
            return false;

        if (_mind.TryGetMind(slime, out _, out var mind) && mind.UserId is { } userId && _players.TryGetSessionById(userId, out _))
            return false;

        comp.FeedTarget = null;
        Dirty(slime, comp);

        var pen = EnsureComp<FishSlimePenStoredComponent>(slime);
        pen.Console = console;
        Dirty(slime, pen);

        _transform.SetCoordinates(slime, Transform(console).Coordinates);
        console.Comp.StoredSlimes.Add(slime);
        Dirty(console);
        return true;
    }

    private void ReleaseAllSlimes(Entity<FishXenobiologyConsoleComponent> console, EntityUid? user)
    {
        var coords = _transform.GetMapCoordinates(console);

        foreach (var slime in console.Comp.StoredSlimes.ToArray())
        {
            RemComp<FishSlimePenStoredComponent>(slime);
            _transform.SetMapCoordinates(slime, coords);
        }

        console.Comp.StoredSlimes.Clear();
        Dirty(console);
    }

    private void DirtyUi(Entity<FishXenobiologyConsoleComponent> ent, string? status = null)
    {
        string? potionName = null;
        if (ent.Comp.LoadedPotionKind is { } kind)
            potionName = kind.ToString();

        _ui.SetUiState(ent, FishXenobiologyConsoleUiKey.Key,
            new FishXenobiologyConsoleBoundUserInterfaceState(
                ent.Comp.StoredSlimes.Count,
                FishXenobiologyConsoleComponent.MaxStoredSlimes,
                ent.Comp.MonkeyCubeCount,
                potionName,
                status));
    }
}
