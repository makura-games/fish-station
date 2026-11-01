using Content.Shared._Fish.Research.Bluespace;
using Content.Shared.Database;

namespace Content.Server._Fish.Research.Bluespace;

public sealed partial class FishBluespaceHarvesterSystem
{
    // Fish-edit: управление принимает только доступные актору сообщения открытого интерфейса.
    private void OnUiOpened(Entity<FishBluespaceHarvesterComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnSetEnabled(Entity<FishBluespaceHarvesterComponent> ent, ref FishBluespaceSetEnabledMessage args)
    {
        TrySetEnabled(ent, args.Enabled, args.Actor);
    }

    private void OnSetPowerMode(Entity<FishBluespaceHarvesterComponent> ent, ref FishBluespaceSetPowerModeMessage args)
    {
        TrySetPowerMode(ent, args.Mode, args.Actor);
    }

    private void OnSetLootCategory(Entity<FishBluespaceHarvesterComponent> ent, ref FishBluespaceSetLootCategoryMessage args)
    {
        TrySetLootCategory(ent, args.Category, args.Actor);
    }

    /// <summary>Changes the operator switch after validating access and the critical interlock.</summary>
    public bool TrySetEnabled(Entity<FishBluespaceHarvesterComponent> ent, bool enabled, EntityUid user)
    {
        if (!CanSetEnabled(ent, enabled, user))
            return false;

        ent.Comp.Enabled = enabled;
        UpdatePowerDraw(ent);
        UpdateAppearance(ent);
        UpdateUi(ent);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user)} set {ToPrettyString(ent)} enabled to {enabled}.");
        return true;
    }

    /// <summary>Checks operator access and prevents activation during a critical shutdown.</summary>
    public bool CanSetEnabled(Entity<FishBluespaceHarvesterComponent> ent, bool enabled, EntityUid user)
    {
        return CanConfigure(ent, user) && (!enabled || !IsCritical(ent));
    }

    /// <summary>Selects one of the two power modes without directly changing native PowerState fields.</summary>
    public bool TrySetPowerMode(Entity<FishBluespaceHarvesterComponent> ent, FishBluespacePowerMode mode, EntityUid user)
    {
        if (!CanSetPowerMode(ent, mode, user))
            return false;

        ent.Comp.PowerMode = mode;
        UpdatePowerDraw(ent);
        UpdateUi(ent);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user)} set {ToPrettyString(ent)} power mode to {mode}.");
        return true;
    }

    /// <summary>Checks operator access and rejects undefined power-mode values.</summary>
    public bool CanSetPowerMode(Entity<FishBluespaceHarvesterComponent> ent, FishBluespacePowerMode mode, EntityUid user)
    {
        return Enum.IsDefined(mode) && CanConfigure(ent, user);
    }

    /// <summary>Selects an available configured loot category.</summary>
    public bool TrySetLootCategory(Entity<FishBluespaceHarvesterComponent> ent, FishBluespaceLootCategory category, EntityUid user)
    {
        if (!CanSetLootCategory(ent, category, user))
            return false;

        ent.Comp.LootCategory = category;
        ent.Comp.OutputBlocked = false;
        UpdateUi(ent);
        return true;
    }

    /// <summary>Checks operator access, enum validity and the presence of at least one bounded reward.</summary>
    public bool CanSetLootCategory(Entity<FishBluespaceHarvesterComponent> ent, FishBluespaceLootCategory category, EntityUid user)
    {
        return Enum.IsDefined(category) && CanConfigure(ent, user) && GetTotalLootWeight(category) > 0d;
    }

    /// <summary>Requires a live, unpaused actor who can physically manipulate this open interface.</summary>
    public bool CanConfigure(Entity<FishBluespaceHarvesterComponent> ent, EntityUid user)
    {
        return !TerminatingOrDeleted(ent) && !TerminatingOrDeleted(user) &&
               _metaQuery.TryComp(ent, out var machineMeta) && !machineMeta.EntityPaused &&
               _metaQuery.TryComp(user, out var userMeta) && !userMeta.EntityPaused &&
               _ui.IsUiOpen(ent.Owner, FishBluespaceDeviceUiKey.Key, user) &&
               _actionBlocker.CanInteract(user, ent) && _actionBlocker.CanComplexInteract(user) &&
               _interaction.IsAccessible(user, ent.Owner) && _interaction.InRangeUnobstructed(user, ent.Owner);
    }

    private void UpdateUi(Entity<FishBluespaceHarvesterComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, FishBluespaceDeviceUiKey.Key))
            return;

        var received = 0f;
        var requested = 0f;
        if (_powerConsumerQuery.TryComp(ent, out var consumer))
        {
            received = float.IsFinite(consumer.ReceivedPower) ? MathF.Max(0f, consumer.ReceivedPower) : 0f;
            requested = float.IsFinite(consumer.DrawRate) ? MathF.Max(0f, consumer.DrawRate) : 0f;
        }

        var progress = double.IsFinite(ent.Comp.StoredEnergy) && float.IsFinite(ent.Comp.EnergyPerReward) &&
                       ent.Comp.EnergyPerReward > 0f
            ? (float) Math.Clamp(ent.Comp.StoredEnergy / ent.Comp.EnergyPerReward, 0d, 1d)
            : 0f;
        var anchored = _transformQuery.TryComp(ent, out var xform) && xform.Anchored;
        var state = new FishBluespaceDeviceBoundUserInterfaceState(
            FishBluespaceDeviceKind.Harvester,
            ent.Comp.Enabled,
            CanOperate(ent),
            anchored,
            _bluespaceBackground.TryGetScope(ent, out _),
            true,
            IsCritical(ent),
            _bluespaceBackground.GetLevel(ent),
            received,
            requested,
            progress,
            0f,
            ent.Comp.PowerMode,
            ent.Comp.LootCategory,
            ent.Comp.OutputBlocked,
            energyPerReward: ent.Comp.EnergyPerReward);
        _ui.SetUiState(ent.Owner, FishBluespaceDeviceUiKey.Key, state);
    }
}
