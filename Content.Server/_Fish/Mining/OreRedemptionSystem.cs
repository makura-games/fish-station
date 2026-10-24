using System.Linq;
using Content.Server.Lathe;
using Content.Server.Materials;
using Content.Server.Power.Components;
using Content.Shared._Fish.Mining;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Research.Prototypes;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Mining;

/// <summary>
/// Автопоглощение руды у OreProcessor и автопостановка smelting-рецептов в очередь Lathe.
/// Очки шахтёра (SalvageTicket) начисляются существующим PrintTicket в LatheSystem.
/// </summary>
public sealed class OreRedemptionSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> OreTag = "Ore";

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly LatheSystem _lathe = default!;
    [Dependency] private readonly MaterialStorageSystem _materialStorage = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<MobStateComponent> _mobQuery;

    public override void Initialize()
    {
        base.Initialize();

        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _mobQuery = GetEntityQuery<MobStateComponent>();

        SubscribeLocalEvent<OreRedemptionMachineComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OreRedemptionMachineComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<OreRedemptionMachineComponent, GetDumpableVerbEvent>(OnGetDumpableVerb);
        SubscribeLocalEvent<OreRedemptionMachineComponent, DumpEvent>(OnDump);
    }

    private void OnMapInit(Entity<OreRedemptionMachineComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextScan = _timing.CurTime;
    }

    private void OnExamined(Entity<OreRedemptionMachineComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("ore-redemption-examine"));
    }

    private void OnGetDumpableVerb(Entity<OreRedemptionMachineComponent> ent, ref GetDumpableVerbEvent args)
    {
        args.Verb = Loc.GetString("ore-redemption-dump-verb", ("machine", ent.Owner));
    }

    private void OnDump(Entity<OreRedemptionMachineComponent> ent, ref DumpEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(ent.Owner, out MaterialStorageComponent? storage))
            return;

        if (!IsPowered(ent.Owner))
            return;

        args.Handled = true;

        var absorbed = 0;
        while (args.DumpQueue.TryDequeue(out var ore))
        {
            if (TryAbsorbOre(ent.Owner, ore, storage, ent.Comp))
                absorbed++;
        }

        if (absorbed > 0)
        {
            args.PlaySound = true;
            PlayAbsorbFeedback(ent);
            if (ent.Comp.AutoProcess)
                TryAutoProcess(ent.Owner);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<OreRedemptionMachineComponent, MaterialStorageComponent, TransformComponent>();
        var now = _timing.CurTime;

        while (query.MoveNext(out var uid, out var redemption, out var storage, out _))
        {
            if (redemption.NextScan > now)
                continue;

            redemption.NextScan = now + redemption.ScanDelay;
            Dirty(uid, redemption);

            if (!IsPowered(uid))
                continue;

            var absorbed = ScanAndAbsorb(uid, redemption, storage);
            if (absorbed <= 0)
                continue;

            PlayAbsorbFeedback((uid, redemption));
            if (redemption.AutoProcess)
                TryAutoProcess(uid);
        }
    }

    /// <summary>
    /// Сканирует область и поглощает руду / опустошает рудные контейнеры.
    /// </summary>
    public int ScanAndAbsorb(EntityUid machine, OreRedemptionMachineComponent redemption, MaterialStorageComponent storage)
    {
        if (!CanAbsorb(machine, redemption, quiet: true))
            return 0;

        var absorbed = 0;
        var remaining = redemption.MaxAbsorbPerScan;

        foreach (var near in _lookup.GetEntitiesInRange(machine, redemption.Range, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (remaining <= 0)
                break;

            if (near == machine)
                continue;

            // Рудный контейнер (OreBox / сброшенный OreBag) — опустошаем содержимое.
            if (TryComp(near, out StorageComponent? nearStorage) && IsOreStorage(nearStorage) && !IsHeldOrWorn(near))
            {
                absorbed += DrainOreStorage(machine, near, nearStorage, storage, redemption, ref remaining);
                continue;
            }

            if (!CanAbsorbLooseOre(near, redemption, storage))
                continue;

            if (!TryAbsorbOre(machine, near, storage, redemption))
                continue;

            absorbed++;
            remaining--;
        }

        return absorbed;
    }

    public bool TryAbsorbOre(
        EntityUid machine,
        EntityUid ore,
        MaterialStorageComponent storage,
        OreRedemptionMachineComponent? redemption = null)
    {
        if (!Resolve(machine, ref redemption, false))
            return false;

        if (!CanAbsorb(machine, redemption, quiet: true))
            return false;

        if (!TryComp(ore, out PhysicalCompositionComponent? composition) ||
            !TryComp(ore, out MaterialComponent? material))
            return false;

        var absorbWhitelist = redemption.Whitelist ?? storage.Whitelist;
        if (_whitelist.IsWhitelistFail(absorbWhitelist, ore))
            return false;

        if (HasComp<UnremoveableComponent>(ore))
            return false;

        var multiplier = TryComp(ore, out StackComponent? stack) ? stack.Count : 1;
        var materials = new Dictionary<string, int>();
        foreach (var (mat, vol) in composition.MaterialComposition)
        {
            var amount = vol * multiplier;
            if (!_materialStorage.CanChangeMaterialAmount(machine, mat, amount, storage))
                return false;

            materials[mat] = materials.GetValueOrDefault(mat) + amount;
        }

        if (materials.Count == 0)
            return false;

        if (!_materialStorage.TryChangeMaterialAmount((machine, storage), materials))
            return false;

        // Анимация вставки, как у MaterialStorage.
        var inserting = EnsureComp<InsertingMaterialStorageComponent>(machine);
        inserting.EndTime = _timing.CurTime + storage.InsertionTime;
        if (!storage.IgnoreColor)
        {
            _prototype.TryIndex(composition.MaterialComposition.Keys.First(), out MaterialPrototype? lastMat);
            inserting.MaterialColor = lastMat?.Color;
        }

        _appearance.SetData(machine, MaterialStorageVisuals.Inserting, true);
        Dirty(machine, inserting);

        var ev = new MaterialEntityInsertedEvent(material);
        RaiseLocalEvent(machine, ref ev);

        QueueDel(ore);
        return true;
    }

    /// <summary>
    /// Ставит в очередь все доступные ticketed smelting-рецепты по текущим материалам.
    /// </summary>
    public void TryAutoProcess(EntityUid machine, LatheComponent? lathe = null)
    {
        if (!Resolve(machine, ref lathe))
            return;

        if (!IsPowered(machine))
            return;

        if (!_lathe.TryGetAvailableRecipes(machine, out var recipeIds, lathe))
            return;

        // Сначала однокомпонентные рецепты, чтобы не «съедать» сырьё дорогими сплавами раньше времени.
        var recipes = recipeIds
            .Select(id => _prototype.Index(id))
            .Where(r => r.PrintTicket && r.Result != null)
            .OrderBy(r => r.Materials.Count)
            .ThenBy(r => r.ID)
            .ToList();

        var queuedAny = false;
        foreach (var recipe in recipes)
        {
            var amount = GetMaxProduceable(machine, recipe, lathe);
            if (amount <= 0)
                continue;

            if (!_lathe.TryAddToQueue(machine, recipe, amount, lathe))
                continue;

            queuedAny = true;
        }

        if (queuedAny)
            _lathe.TryStartProducing(machine, lathe);
    }

    private int DrainOreStorage(
        EntityUid machine,
        EntityUid storageUid,
        StorageComponent storageComp,
        MaterialStorageComponent materialStorage,
        OreRedemptionMachineComponent redemption,
        ref int remaining)
    {
        var absorbed = 0;
        // Копия списка — содержимое меняется при QueueDel.
        var contents = storageComp.Container.ContainedEntities.ToArray();
        foreach (var ore in contents)
        {
            if (remaining <= 0)
                break;

            if (!TryAbsorbOre(machine, ore, materialStorage, redemption))
                continue;

            absorbed++;
            remaining--;
        }

        return absorbed;
    }

    private bool CanAbsorbLooseOre(EntityUid ore, OreRedemptionMachineComponent redemption, MaterialStorageComponent storage)
    {
        if (_container.IsEntityInContainer(ore))
            return false;

        if (!_tag.HasTag(ore, OreTag))
            return false;

        if (_physicsQuery.TryGetComponent(ore, out var physics) && physics.BodyStatus != BodyStatus.OnGround)
            return false;

        var absorbWhitelist = redemption.Whitelist ?? storage.Whitelist;
        return !_whitelist.IsWhitelistFail(absorbWhitelist, ore);
    }

    private static bool IsOreStorage(StorageComponent storage)
    {
        return storage.Whitelist?.Tags?.Contains(OreTag) == true;
    }

    private bool IsHeldOrWorn(EntityUid uid)
    {
        if (!_container.TryGetContainingContainer(uid, out var container))
            return false;

        var owner = container.Owner;
        return HasComp<InventoryComponent>(owner)
               || HasComp<HandsComponent>(owner)
               || _mobQuery.HasComponent(owner);
    }

    private bool CanAbsorb(EntityUid machine, OreRedemptionMachineComponent redemption, bool quiet)
    {
        if (!IsPowered(machine))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("ore-redemption-no-power"), machine);
            return false;
        }

        return true;
    }

    private bool IsPowered(EntityUid uid)
    {
        // NeedsPower=false означает «всегда считается включённым», даже если Powered ещё false.
        if (!TryComp(uid, out ApcPowerReceiverComponent? power))
            return true;

        return power.Powered || !power.NeedsPower;
    }

    private int GetMaxProduceable(EntityUid uid, LatheRecipePrototype recipe, LatheComponent lathe)
    {
        if (!_lathe.CanProduce(uid, recipe, 1, lathe))
            return 0;

        var max = SharedLatheSystem.MaxItemsPerRequest;
        foreach (var (material, needed) in recipe.Materials)
        {
            var adjusted = SharedLatheSystem.AdjustMaterial(needed, recipe.ApplyMaterialDiscount, lathe.MaterialUseMultiplier);
            if (adjusted <= 0)
                continue;

            var available = _materialStorage.GetMaterialAmount(uid, material);
            max = Math.Min(max, available / adjusted);
        }

        return Math.Max(0, max);
    }

    private void PlayAbsorbFeedback(Entity<OreRedemptionMachineComponent> ent)
    {
        if (ent.Comp.AbsorbSound != null)
            _audio.PlayPvs(ent.Comp.AbsorbSound, ent.Owner);

        _popup.PopupEntity(Loc.GetString("ore-redemption-absorbed"), ent.Owner);
    }
}
