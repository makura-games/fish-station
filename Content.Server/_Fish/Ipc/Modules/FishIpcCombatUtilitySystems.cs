using Content.Server.Chat.Systems;
using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Shared._Fish.Ipc.Modules;
using Content.Shared.Actions;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Chat;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Electrocution;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._Fish.Ipc.Modules;

/// <summary>
/// Встроенный дефиб / jumpstarter / инструмент-рука.
/// Экшены вешаются на хост — подписки action-событий тоже на хосте.
/// </summary>
public sealed partial class FishIpcCombatUtilitySystems : EntitySystem
{
    [Dependency] private FishIpcModuleSystem _modules = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private SharedElectrocutionSystem _electrocution = default!;
    [Dependency] private SharedRottingSystem _rotting = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishIpcDefibModuleComponent, FishIpcModuleInstalledEvent>(OnDefibInstall);
        SubscribeLocalEvent<FishIpcDefibModuleComponent, FishIpcModuleUninstalledEvent>(OnDefibUninstall);
        SubscribeLocalEvent<FishIpcDefibModuleComponent, FishIpcDefibDoAfterEvent>(OnDefibDoAfter);

        SubscribeLocalEvent<FishIpcModuleHostComponent, FishIpcDefibActionEvent>(OnDefibAction);
        SubscribeLocalEvent<FishIpcModuleHostComponent, FishIpcJumpstarterActionEvent>(OnJumpAction);

        SubscribeLocalEvent<FishIpcDefibPadsComponent, AfterInteractEvent>(OnPadsInteract);
        SubscribeLocalEvent<FishIpcDefibPadsComponent, ComponentShutdown>(OnPadsShutdown);
        SubscribeLocalEvent<FishIpcDefibPadsComponent, VirtualItemDeletedEvent>(OnPadsVirtualDeleted);

        SubscribeLocalEvent<FishIpcJumpstarterModuleComponent, FishIpcModuleInstalledEvent>(OnJumpInstall);
        SubscribeLocalEvent<FishIpcJumpstarterModuleComponent, FishIpcModuleUninstalledEvent>(OnJumpUninstall);

        SubscribeLocalEvent<FishIpcToolHandModuleComponent, FishIpcModuleInstalledEvent>(OnToolInstall);
        SubscribeLocalEvent<FishIpcToolHandModuleComponent, FishIpcModuleUninstalledEvent>(OnToolUninstall);
        SubscribeLocalEvent<FishIpcToolHandModuleComponent, FishIpcModulePowerToggledEvent>(OnToolPower);
        SubscribeLocalEvent<FishIpcDefibModuleComponent, FishIpcModulePowerToggledEvent>(OnDefibPower);
    }

    private void OnDefibInstall(Entity<FishIpcDefibModuleComponent> ent, ref FishIpcModuleInstalledEvent args)
    {
        _actions.AddAction(args.Host, ref ent.Comp.ActionEntity, ent.Comp.Action);
        if (ent.Comp.ActionEntity is { } action)
            _actions.SetToggled(action, false);
    }

    private void OnDefibUninstall(Entity<FishIpcDefibModuleComponent> ent, ref FishIpcModuleUninstalledEvent args)
    {
        DeactivatePads(ent);
        _actions.RemoveAction(args.Host, ent.Comp.ActionEntity);
        ent.Comp.ActionEntity = null;
    }

    private void OnDefibAction(Entity<FishIpcModuleHostComponent> host, ref FishIpcDefibActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetInstalledModule<FishIpcDefibModuleComponent>(host.Owner, out var module))
            return;

        if (TryComp<FishIpcModulePowerToggleComponent>(module.Owner, out var power) && !power.Enabled)
        {
            _popup.PopupEntity(Loc.GetString("fish-ipc-module-toggled-off"), host, host);
            return;
        }

        args.Handled = true;

        if (module.Comp.PadsActive)
        {
            DeactivatePads(module);
            if (module.Comp.ActionEntity is { } actionOff)
                _actions.SetToggled(actionOff, false);
            return;
        }

        if (!ActivatePads(host.Owner, module))
            return;

        if (module.Comp.ActionEntity is { } actionOn)
            _actions.SetToggled(actionOn, true);
    }

    private bool ActivatePads(EntityUid host, Entity<FishIpcDefibModuleComponent> module)
    {
        if (module.Comp.PadsEntity is { } existing && !TerminatingOrDeleted(existing))
            return true;

        var pads = Spawn(module.Comp.PadsPrototype, Transform(host).Coordinates);
        var padsComp = EnsureComp<FishIpcDefibPadsComponent>(pads);
        padsComp.Module = module.Owner;

        EnsureComp<UnremoveableComponent>(pads);
        EnsureComp<MultiHandedItemComponent>(pads);

        // Свободных рук нет — выбросить предмет и занять руку, иначе модуль «включён» без накладок.
        if (!_hands.TryForcePickupAnyHand(host, pads, checkActionBlocker: false))
        {
            QueueDel(pads);
            _popup.PopupEntity(Loc.GetString("fish-ipc-module-tool-no-hand"), host, host);
            return false;
        }

        module.Comp.PadsEntity = pads;
        module.Comp.PadsActive = true;
        return true;
    }

    private void DeactivatePads(Entity<FishIpcDefibModuleComponent> module)
    {
        module.Comp.PadsActive = false;
        if (module.Comp.PadsEntity is not { } pads)
            return;

        module.Comp.PadsEntity = null;
        if (!TerminatingOrDeleted(pads))
            QueueDel(pads);
    }

    private void OnPadsShutdown(Entity<FishIpcDefibPadsComponent> pads, ref ComponentShutdown args)
    {
        if (pads.Comp.Module is not { } moduleUid ||
            !TryComp<FishIpcDefibModuleComponent>(moduleUid, out var module))
            return;

        module.PadsActive = false;
        module.PadsEntity = null;
        if (module.ActionEntity is { } action)
            _actions.SetToggled(action, false);
    }

    private void OnPadsVirtualDeleted(Entity<FishIpcDefibPadsComponent> pads, ref VirtualItemDeletedEvent args)
    {
        // Накладки Unremoveable — если виртуалка слетела, сразу вернуть вторую руку.
        if (TerminatingOrDeleted(pads.Owner))
            return;

        if (!_hands.IsHolding(args.User, pads.Owner))
            return;

        _virtualItem.TrySpawnVirtualItemInHand(pads.Owner, args.User);
    }

    private void OnPadsInteract(Entity<FishIpcDefibPadsComponent> pads, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (pads.Comp.Module is not { } moduleUid ||
            !TryComp<FishIpcDefibModuleComponent>(moduleUid, out var moduleComp) ||
            !TryComp<FishIpcModuleComponent>(moduleUid, out var mod) ||
            !mod.Installed ||
            mod.InstalledEntity is not { } host)
            return;

        if (target == host)
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("fish-ipc-module-defib-self"), host, host);
            return;
        }

        if (!HasEnoughCharge(host, moduleComp.ChargeFraction))
        {
            _popup.PopupEntity(Loc.GetString("fish-ipc-module-need-charge"), host, host, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;
        _audio.PlayPvs(moduleComp.ChargeSound, host);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, host, moduleComp.DoAfter, new FishIpcDefibDoAfterEvent(), moduleUid, target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DistanceThreshold = 1.5f,
        });
    }

    private void OnDefibDoAfter(Entity<FishIpcDefibModuleComponent> ent, ref FishIpcDefibDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        if (!TryComp<FishIpcModuleComponent>(ent.Owner, out var moduleComp) || !moduleComp.Installed ||
            moduleComp.InstalledEntity is not { } host)
            return;

        var cost = GetChargeCost(host, ent.Comp.ChargeFraction);
        if (!_modules.TryConsumeModuleCharge(host, cost))
        {
            _popup.PopupEntity(Loc.GetString("fish-ipc-module-need-charge"), host, host, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        if (ent.Comp.ActionEntity is { } action)
            _actions.SetCooldown(action, ent.Comp.Cooldown);

        _audio.PlayPvs(ent.Comp.ZapSound, host);

        if (_rotting.IsRotten(target) || HasComp<UnrevivableComponent>(target))
        {
            _chat.TrySendInGameICMessage(host, Loc.GetString("defibrillator-rotten"), InGameICChatType.Speak, true);
            _electrocution.TryDoElectrocution(target, host, 5, TimeSpan.FromSeconds(2), true, ignoreInsulation: true);
            _audio.PlayPvs(ent.Comp.FailureSound, host);
            return;
        }

        _damageable.TryChangeDamage(target, ent.Comp.ZapHeal, true, origin: host);

        var failed = true;
        if (_mobState.IsDead(target) &&
            TryComp<MobThresholdsComponent>(target, out var thresholds) &&
            _mobThreshold.TryGetThresholdForState(target, MobState.Dead, out var deadThreshold, thresholds) &&
            _damageable.GetTotalDamage(target) < deadThreshold)
        {
            _mobState.ChangeMobState(target, MobState.Critical, null, host);
            failed = false;
        }

        if (_mind.TryGetMind(target, out _, out var mindComp) &&
            mindComp.UserId is { } userId &&
            _player.TryGetSessionById(userId, out var session) &&
            mindComp.CurrentEntity != target)
        {
            _eui.OpenEui(new ReturnToBodyEui(mindComp, _mind, _player), session);
        }

        _electrocution.TryDoElectrocution(target, host, 5, TimeSpan.FromSeconds(3), true, ignoreInsulation: true);
        _audio.PlayPvs(failed ? ent.Comp.FailureSound : ent.Comp.SuccessSound, host);
        _popup.PopupEntity(
            Loc.GetString(failed ? "fish-ipc-module-defib-fail" : "fish-ipc-module-defib-success",
                ("target", Identity.Entity(target, EntityManager))),
            host,
            host);
    }

    private void OnJumpInstall(Entity<FishIpcJumpstarterModuleComponent> ent, ref FishIpcModuleInstalledEvent args)
    {
        _actions.AddAction(args.Host, ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnJumpUninstall(Entity<FishIpcJumpstarterModuleComponent> ent, ref FishIpcModuleUninstalledEvent args)
    {
        _actions.RemoveAction(args.Host, ent.Comp.ActionEntity);
        ent.Comp.ActionEntity = null;
    }

    private void OnJumpAction(Entity<FishIpcModuleHostComponent> host, ref FishIpcJumpstarterActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetInstalledModule<FishIpcJumpstarterModuleComponent>(host.Owner, out var module))
            return;

        if (TryComp<FishIpcModulePowerToggleComponent>(module.Owner, out var power) && !power.Enabled)
        {
            _popup.PopupEntity(Loc.GetString("fish-ipc-module-toggled-off"), host, host);
            return;
        }

        if (!_mobState.IsCritical(host.Owner))
        {
            _popup.PopupEntity(Loc.GetString("fish-ipc-module-jumpstarter-not-crit"), host, host);
            return;
        }

        var cost = GetChargeCost(host.Owner, module.Comp.ChargeFraction);
        if (!_modules.TryConsumeModuleCharge(host.Owner, cost))
        {
            _popup.PopupEntity(Loc.GetString("fish-ipc-module-need-charge"), host, host, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        if (module.Comp.ActionEntity is { } action)
            _actions.SetCooldown(action, module.Comp.Cooldown);

        _damageable.TryChangeDamage(host.Owner, module.Comp.HealOnUse, true, origin: host.Owner);

        if (TryComp<MobThresholdsComponent>(host.Owner, out var thresholds) &&
            _mobThreshold.TryGetThresholdForState(host.Owner, MobState.Dead, out var deadThreshold, thresholds) &&
            _damageable.GetTotalDamage(host.Owner) >= deadThreshold)
        {
            _popup.PopupEntity(Loc.GetString("fish-ipc-module-jumpstarter-fail"), host, host, PopupType.MediumCaution);
            return;
        }

        _mobState.ChangeMobState(host.Owner, MobState.Alive);
        _popup.PopupEntity(Loc.GetString("fish-ipc-module-jumpstarter-success"), host, host, PopupType.Medium);
    }

    private void OnToolPower(Entity<FishIpcToolHandModuleComponent> ent, ref FishIpcModulePowerToggledEvent args)
    {
        if (args.Enabled)
        {
            var installed = new FishIpcModuleInstalledEvent(args.Host);
            OnToolInstall(ent, ref installed);
            return;
        }

        var removed = new FishIpcModuleUninstalledEvent(args.Host);
        OnToolUninstall(ent, ref removed);
    }

    private void OnDefibPower(Entity<FishIpcDefibModuleComponent> ent, ref FishIpcModulePowerToggledEvent args)
    {
        if (!args.Enabled)
            DeactivatePads(ent);
    }

    private void OnToolInstall(Entity<FishIpcToolHandModuleComponent> ent, ref FishIpcModuleInstalledEvent args)
    {
        if (ent.Comp.SpawnedTool is { } existing && !TerminatingOrDeleted(existing))
            return;

        var tool = Spawn(ent.Comp.ToolPrototype, Transform(args.Host).Coordinates);
        EnsureComp<UnremoveableComponent>(tool);
        // Модуль уже установлен — инструмент обязан попасть в руку (иначе «пустой» апгрейд).
        if (!_hands.TryForcePickupAnyHand(args.Host, tool, checkActionBlocker: false))
        {
            QueueDel(tool);
            _popup.PopupEntity(Loc.GetString("fish-ipc-module-tool-no-hand"), args.Host, args.Host);
            ent.Comp.SpawnedTool = null;
            return;
        }

        ent.Comp.SpawnedTool = tool;
    }

    private void OnToolUninstall(Entity<FishIpcToolHandModuleComponent> ent, ref FishIpcModuleUninstalledEvent args)
    {
        if (ent.Comp.SpawnedTool is not { } tool)
            return;

        if (!TerminatingOrDeleted(tool))
            QueueDel(tool);

        ent.Comp.SpawnedTool = null;
    }

    private bool TryGetInstalledModule<T>(EntityUid host, out Entity<T> module) where T : IComponent
    {
        module = default;
        if (!TryComp<FishIpcModuleHostComponent>(host, out var hostComp))
            return false;

        foreach (var uid in hostComp.ModuleContainer.ContainedEntities)
        {
            if (!TryComp<T>(uid, out var comp))
                continue;

            if (!TryComp<FishIpcModuleComponent>(uid, out var mod) || !mod.Installed)
                continue;

            module = (uid, comp);
            return true;
        }

        return false;
    }

    private bool HasEnoughCharge(EntityUid host, float fraction) =>
        _modules.GetHostChargeFraction(host) >= fraction;

    private float GetChargeCost(EntityUid host, float fraction)
    {
        var max = _modules.GetHostMaxCharge(host);
        return max > 0f ? max * fraction : float.MaxValue;
    }
}
