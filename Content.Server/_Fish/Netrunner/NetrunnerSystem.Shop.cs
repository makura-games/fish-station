using Content.Shared._Fish.Netrunner;
using Content.Shared.Database;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Netrunner;

public sealed partial class NetrunnerSystem
{
    // Fish-edit: Покупки и исследовательские связи принадлежат импланту, а не текущему телу.
    private void InitializeShop()
    {
        Subs.BuiEvents<NetrunnerImplantComponent>(NetrunnerUiKey.Shop, subs =>
        {
            subs.Event<NetrunnerBuyProgramMessage>(OnBuyProgram);
            subs.Event<NetrunnerBuyUpgradeMessage>(OnBuyUpgrade);
            subs.Event<NetrunnerBuyVisorMessage>(OnBuyVisor);
        });
    }

    private void OnOpenShop(Entity<NetrunnerImplantComponent> ent, ref NetrunnerOpenShopMessage args)
        => TryOpenShop(ent, args.Actor);

    private void OnBuyProgram(Entity<NetrunnerImplantComponent> ent, ref NetrunnerBuyProgramMessage args)
        => TryBuyProgram(ent, args.Actor, args.Ability);

    private void OnBuyUpgrade(Entity<NetrunnerImplantComponent> ent, ref NetrunnerBuyUpgradeMessage args)
        => TryBuyUpgrade(ent, args.Actor, args.Upgrade);

    private void OnBuyVisor(Entity<NetrunnerImplantComponent> ent, ref NetrunnerBuyVisorMessage args)
        => TryBuyVisor(ent, args.Actor);

    /// <summary>Checks whether the operator can open the private script shop.</summary>
    public bool CanOpenShop(Entity<NetrunnerImplantComponent> ent, EntityUid user)
        => IsOperator(ent, user);

    /// <summary>Opens the private shop for the living, authorized implant holder.</summary>
    public bool TryOpenShop(Entity<NetrunnerImplantComponent> ent, EntityUid user)
    {
        if (!CanOpenShop(ent, user) || !_ui.TryOpenUi(ent.Owner, NetrunnerUiKey.Shop, user))
            return false;
        PublishShop(ent);
        return true;
    }

    /// <summary>Purchases and grants one program, charging only after the action was created.</summary>
    public bool TryBuyProgram(Entity<NetrunnerImplantComponent> ent, EntityUid user,
        ProtoId<NetrunnerAbilityPrototype> id)
    {
        if (!CanBuyProgram(ent, user, id) || !GrantProgram(ent, user, id))
        {
            PurchaseRejected(ent, user);
            return false;
        }
        var price = ProtoMan.Index(id).LinkCost;
        if (!ent.Comp.DebugMode)
            ent.Comp.NeuralLinks -= price;
        ent.Comp.UnlockedAbilities.Add(id);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):user} purchased netrunner program {id} for {price} neural links");
        Publish(ent);
        return true;
    }

    /// <summary>Rejects unknown programs, duplicate purchases, insufficient credit and in-flight work.</summary>
    public bool CanBuyProgram(Entity<NetrunnerImplantComponent> ent, EntityUid user,
        ProtoId<NetrunnerAbilityPrototype> id)
    {
        return CanPurchase(ent, user) && ent.Comp.Abilities.Contains(id)
            && !ent.Comp.UnlockedAbilities.Contains(id) && ProtoMan.TryIndex(id, out var ability)
            && ability.LinkCost >= 0 && (ent.Comp.DebugMode || ent.Comp.NeuralLinks >= ability.LinkCost);
    }

    /// <summary>Installs one capped hardware upgrade. Added memory begins empty.</summary>
    public bool TryBuyUpgrade(Entity<NetrunnerImplantComponent> ent, EntityUid user, NetrunnerUpgrade upgrade)
    {
        if (!CanBuyUpgrade(ent, user, upgrade))
        {
            PurchaseRejected(ent, user);
            return false;
        }
        var price = GetUpgradePrice(ent, upgrade);
        if (!ent.Comp.DebugMode)
            ent.Comp.NeuralLinks -= price;
        switch (upgrade)
        {
            case NetrunnerUpgrade.Memory:
                ent.Comp.MemoryUpgrades++;
                ent.Comp.MaxRam += ent.Comp.MemoryPerUpgrade;
                break;
            case NetrunnerUpgrade.ActivationSpeed:
                ent.Comp.SpeedUpgrades++;
                break;
        }
        if (ent.Comp.DebugMode)
            ent.Comp.Ram = ent.Comp.MaxRam;
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):user} purchased netrunner upgrade {upgrade} for {price} neural links");
        Publish(ent);
        return true;
    }

    /// <summary>Checks the enum, upgrade cap and server-calculated price without mutating state.</summary>
    public bool CanBuyUpgrade(Entity<NetrunnerImplantComponent> ent, EntityUid user, NetrunnerUpgrade upgrade)
    {
        if (!CanPurchase(ent, user))
            return false;
        var available = upgrade switch
        {
            NetrunnerUpgrade.Memory => ent.Comp.MemoryUpgrades < ent.Comp.MaxMemoryUpgrades,
            NetrunnerUpgrade.ActivationSpeed => ent.Comp.SpeedUpgrades < ent.Comp.MaxSpeedUpgrades,
            _ => false,
        };
        var price = GetUpgradePrice(ent, upgrade);
        return available && price >= 0 && (ent.Comp.DebugMode || ent.Comp.NeuralLinks >= price);
    }

    /// <summary>Returns the effective, bounded activation duration for a purchased program.</summary>
    public TimeSpan GetCastTime(Entity<NetrunnerImplantComponent> ent, NetrunnerAbilityPrototype ability)
    {
        if (ent.Comp.DebugMode)
            return TimeSpan.Zero;
        var seconds = ability.CastTime.TotalSeconds * GetCastMultiplier(ent);
        return TimeSpan.FromSeconds(Math.Max(seconds, ability.MinCastTime.TotalSeconds));
    }

    private bool CanPurchase(Entity<NetrunnerImplantComponent> ent, EntityUid user)
        => IsOperator(ent, user) && ent.Comp.Pending == null && ent.Comp.Connection == null;

    private static float GetCastMultiplier(Entity<NetrunnerImplantComponent> ent)
        => Math.Clamp(1 - ent.Comp.SpeedUpgrades * ent.Comp.SpeedPerUpgrade, 0.5f, 1);

    private static int GetUpgradePrice(Entity<NetrunnerImplantComponent> ent, NetrunnerUpgrade upgrade)
    {
        var level = upgrade == NetrunnerUpgrade.Memory ? ent.Comp.MemoryUpgrades : ent.Comp.SpeedUpgrades;
        return ent.Comp.UpgradeBaseCost + level * ent.Comp.UpgradeCostStep;
    }

    private bool GrantProgram(Entity<NetrunnerImplantComponent> ent, EntityUid user,
        ProtoId<NetrunnerAbilityPrototype> id)
    {
        if (!ProtoMan.TryIndex(id, out var ability))
            return false;
        EntityUid? action = null;
        _actions.AddAction(user, ref action, ability.Action, ent);
        if (action is not { } granted)
            return false;
        ent.Comp.GrantedActions.Add(granted);
        _actions.SetUseDelay(granted, GetCooldown(ent, ability));
        var readyAt = ent.Comp.Cooldowns.GetValueOrDefault(id);
        if (readyAt > _timing.CurTime)
            _actions.SetCooldown(granted, _timing.CurTime, readyAt);
        return true;
    }

    private void PurchaseRejected(Entity<NetrunnerImplantComponent> ent, EntityUid user)
    {
        if (IsOperator(ent, user))
            _popup.PopupEntity(Loc.GetString("fish-netrunner-shop-unavailable"), user, user);
        PublishShop(ent);
    }

    private void RewardResearch(Entity<NetrunnerImplantComponent> ent, EntityUid device)
    {
        if (!ent.Comp.ResearchedDevices.Add(device))
            return;
        ent.Comp.NeuralLinks++;
    }

    private void PublishShop(Entity<NetrunnerImplantComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, NetrunnerUiKey.Shop) || ent.Comp.User is not { } user)
            return;
        var snapshot = new NetrunnerShopState
        {
            NeuralLinks = ent.Comp.NeuralLinks,
            MaxRam = ent.Comp.MaxRam,
            MemoryLevel = ent.Comp.MemoryUpgrades,
            MaxMemoryLevel = ent.Comp.MaxMemoryUpgrades,
            MemoryPrice = GetUpgradePrice(ent, NetrunnerUpgrade.Memory),
            MemoryGain = ent.Comp.MemoryPerUpgrade,
            SpeedLevel = ent.Comp.SpeedUpgrades,
            MaxSpeedLevel = ent.Comp.MaxSpeedUpgrades,
            SpeedPrice = GetUpgradePrice(ent, NetrunnerUpgrade.ActivationSpeed),
            CastMultiplier = GetCastMultiplier(ent),
            SpeedGain = ent.Comp.SpeedPerUpgrade,
            CanBuy = CanPurchase(ent, user),
            DebugMode = ent.Comp.DebugMode,
            VisorPrice = ent.Comp.VisorPrice,
            VisorPurchased = ent.Comp.VisorPurchased,
        };
        foreach (var id in ent.Comp.Abilities)
        {
            var ability = ProtoMan.Index(id);
            snapshot.Programs.Add(new NetrunnerShopEntry(id, ability.Kind, ability.LinkCost,
                ent.Comp.UnlockedAbilities.Contains(id), ability.RamCost, ability.TraceCost,
                (float)GetCastTime(ent, ability).TotalSeconds, (float)ability.Duration.TotalSeconds,
                (float)GetCooldown(ent, ability).TotalSeconds));
        }
        _ui.SetUiState(ent.Owner, NetrunnerUiKey.Shop, snapshot);
    }

    /// <summary>Creates one physical visor after validating the holder and available credit.</summary>
    public bool TryBuyVisor(Entity<NetrunnerImplantComponent> ent, EntityUid user)
    {
        if (!CanBuyVisor(ent, user))
        {
            PurchaseRejected(ent, user);
            return false;
        }
        var visor = Spawn("FishClothingEyesNetrunnerVisor", Transform(user).Coordinates);
        ent.Comp.VisorPurchased = true;
        if (!ent.Comp.DebugMode)
            ent.Comp.NeuralLinks -= ent.Comp.VisorPrice;
        _hands.TryPickupAnyHand(user, visor);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):user} purchased netrunner visor for {ent.Comp.VisorPrice} neural links; debug: {ent.Comp.DebugMode}");
        _popup.PopupEntity(Loc.GetString("fish-netrunner-visor-delivered"), user, user);
        Publish(ent);
        return true;
    }

    /// <summary>Checks the purchase limit and server-configured price without changing state.</summary>
    public bool CanBuyVisor(Entity<NetrunnerImplantComponent> ent, EntityUid user)
        => CanPurchase(ent, user) && !ent.Comp.VisorPurchased && ent.Comp.VisorPrice >= 0
            && (ent.Comp.DebugMode || ent.Comp.NeuralLinks >= ent.Comp.VisorPrice);
}
