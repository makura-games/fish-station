using Content.Shared._Fish.Ipc;
using Content.Shared._Fish.Ipc.Modules;
using Content.Shared._Fish.Vision;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Overlays;
using Content.Shared.Popups;

namespace Content.Server._Fish.Ipc.Modules;

/// <summary>
/// Установка/снятие маркеров хоста и toggle-actions пассивных модулей.
/// </summary>
public sealed partial class FishIpcModuleGrantSystem : EntitySystem
{
    [Dependency] private FishIpcRadioSystem _radio = default!;
    [Dependency] private FishIpcModuleSystem _modules = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishIpcEmpScreenModuleComponent, FishIpcModuleInstalledEvent>(OnEmpScreenInstall);
        SubscribeLocalEvent<FishIpcEmpScreenModuleComponent, FishIpcModuleUninstalledEvent>(OnEmpScreenUninstall);

        SubscribeLocalEvent<FishIpcApcAdapterModuleComponent, FishIpcModuleInstalledEvent>(OnApcInstall);
        SubscribeLocalEvent<FishIpcApcAdapterModuleComponent, FishIpcModuleUninstalledEvent>(OnApcUninstall);

        SubscribeLocalEvent<FishIpcRadFilterModuleComponent, FishIpcModuleInstalledEvent>(OnRadInstall);
        SubscribeLocalEvent<FishIpcRadFilterModuleComponent, FishIpcModuleUninstalledEvent>(OnRadUninstall);

        SubscribeLocalEvent<FishIpcVocalModModuleComponent, FishIpcModuleInstalledEvent>(OnVocalInstall);
        SubscribeLocalEvent<FishIpcVocalModModuleComponent, FishIpcModuleUninstalledEvent>(OnVocalUninstall);

        SubscribeLocalEvent<FishIpcWireInterfaceModuleComponent, FishIpcModuleInstalledEvent>(OnWireInstall);
        SubscribeLocalEvent<FishIpcWireInterfaceModuleComponent, FishIpcModuleUninstalledEvent>(OnWireUninstall);

        SubscribeLocalEvent<FishIpcVisionModuleComponent, FishIpcModuleInstalledEvent>(OnVisionInstall);
        SubscribeLocalEvent<FishIpcVisionModuleComponent, FishIpcModuleUninstalledEvent>(OnVisionUninstall);

        SubscribeLocalEvent<FishIpcPassiveHealModuleComponent, FishIpcModuleInstalledEvent>(OnHealInstall);
        SubscribeLocalEvent<FishIpcPassiveHealModuleComponent, FishIpcModuleUninstalledEvent>(OnHealUninstall);

        SubscribeLocalEvent<FishIpcModulePowerToggleComponent, FishIpcModuleInstalledEvent>(OnPowerToggleInstall);
        SubscribeLocalEvent<FishIpcModulePowerToggleComponent, FishIpcModuleUninstalledEvent>(OnPowerToggleUninstall);

        SubscribeLocalEvent<FishIpcModuleHostComponent, FishIpcModuleToggleActionEvent>(OnModuleToggle);
        SubscribeLocalEvent<FishIpcModuleHostComponent, FishIpcChassisPowerChangedEvent>(OnChassisPower);

        SubscribeLocalEvent<FishIpcRadFilterHostComponent, DamageModifyEvent>(OnRadModify);
        SubscribeLocalEvent<FishIpcWireInterfaceHostComponent, ExaminedEvent>(OnWireExamineHost);
    }

    private void OnEmpScreenInstall(Entity<FishIpcEmpScreenModuleComponent> ent, ref FishIpcModuleInstalledEvent args) =>
        SyncModuleEffects(ent.Owner, args.Host);

    private void OnEmpScreenUninstall(Entity<FishIpcEmpScreenModuleComponent> ent, ref FishIpcModuleUninstalledEvent args) =>
        SyncModuleEffects(ent.Owner, args.Host);

    private void OnApcInstall(Entity<FishIpcApcAdapterModuleComponent> ent, ref FishIpcModuleInstalledEvent args) =>
        SyncModuleEffects(ent.Owner, args.Host);

    private void OnApcUninstall(Entity<FishIpcApcAdapterModuleComponent> ent, ref FishIpcModuleUninstalledEvent args) =>
        SyncModuleEffects(ent.Owner, args.Host);

    private void OnRadInstall(Entity<FishIpcRadFilterModuleComponent> ent, ref FishIpcModuleInstalledEvent args) =>
        SyncModuleEffects(ent.Owner, args.Host);

    private void OnRadUninstall(Entity<FishIpcRadFilterModuleComponent> ent, ref FishIpcModuleUninstalledEvent args) =>
        SyncModuleEffects(ent.Owner, args.Host);

    private void OnVocalInstall(Entity<FishIpcVocalModModuleComponent> ent, ref FishIpcModuleInstalledEvent args) =>
        SyncModuleEffects(ent.Owner, args.Host);

    private void OnVocalUninstall(Entity<FishIpcVocalModModuleComponent> ent, ref FishIpcModuleUninstalledEvent args) =>
        SyncModuleEffects(ent.Owner, args.Host);

    private void OnWireInstall(Entity<FishIpcWireInterfaceModuleComponent> ent, ref FishIpcModuleInstalledEvent args) =>
        SyncModuleEffects(ent.Owner, args.Host);

    private void OnWireUninstall(Entity<FishIpcWireInterfaceModuleComponent> ent, ref FishIpcModuleUninstalledEvent args) =>
        SyncModuleEffects(ent.Owner, args.Host);

    private void OnVisionInstall(Entity<FishIpcVisionModuleComponent> ent, ref FishIpcModuleInstalledEvent args)
    {
        _actions.AddAction(args.Host, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
        if (ent.Comp.ToggleActionEntity is { } action)
            _actions.SetToggled(action, ent.Comp.Enabled);

        SyncModuleEffects(ent.Owner, args.Host);
    }

    private void OnVisionUninstall(Entity<FishIpcVisionModuleComponent> ent, ref FishIpcModuleUninstalledEvent args)
    {
        SyncModuleEffects(ent.Owner, args.Host);
        _actions.RemoveAction(args.Host, ent.Comp.ToggleActionEntity);
        ent.Comp.ToggleActionEntity = null;
    }

    private void OnHealInstall(Entity<FishIpcPassiveHealModuleComponent> ent, ref FishIpcModuleInstalledEvent args)
    {
        _actions.AddAction(args.Host, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
        if (ent.Comp.ToggleActionEntity is { } action)
            _actions.SetToggled(action, ent.Comp.Enabled);
    }

    private void OnHealUninstall(Entity<FishIpcPassiveHealModuleComponent> ent, ref FishIpcModuleUninstalledEvent args)
    {
        _actions.RemoveAction(args.Host, ent.Comp.ToggleActionEntity);
        ent.Comp.ToggleActionEntity = null;
    }

    private void OnPowerToggleInstall(Entity<FishIpcModulePowerToggleComponent> ent, ref FishIpcModuleInstalledEvent args)
    {
        _actions.AddAction(args.Host, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
        if (ent.Comp.ToggleActionEntity is not { } action)
            return;

        _actions.SetToggled(action, ent.Comp.Enabled);
        _actions.SetEntityIcon(action, ent.Owner);
        var meta = MetaData(action);
        _meta.SetEntityName(action, Loc.GetString("fish-ipc-module-toggle-name", ("module", Name(ent.Owner))), meta);
        _meta.SetEntityDescription(action, Loc.GetString("fish-ipc-module-toggle-desc"), meta);
    }

    private void OnPowerToggleUninstall(Entity<FishIpcModulePowerToggleComponent> ent, ref FishIpcModuleUninstalledEvent args)
    {
        _actions.RemoveAction(args.Host, ent.Comp.ToggleActionEntity);
        ent.Comp.ToggleActionEntity = null;
    }

    private void OnModuleToggle(Entity<FishIpcModuleHostComponent> host, ref FishIpcModuleToggleActionEvent args)
    {
        if (args.Handled)
            return;

        var action = args.Action.Owner;

        foreach (var uid in host.Comp.ModuleContainer.ContainedEntities)
        {
            if (TryComp<FishIpcPassiveHealModuleComponent>(uid, out var heal) &&
                heal.ToggleActionEntity == action)
            {
                heal.Enabled = !heal.Enabled;
                Dirty(uid, heal);
                _actions.SetToggled(action, heal.Enabled);
                NotifyModuleToggle(host.Owner, uid, heal.Enabled);
                args.Handled = true;
                return;
            }

            if (TryComp<FishIpcVisionModuleComponent>(uid, out var vision) &&
                vision.ToggleActionEntity == action)
            {
                vision.Enabled = !vision.Enabled;
                Dirty(uid, vision);
                SyncModuleEffects(uid, host.Owner);
                _actions.SetToggled(action, vision.Enabled);
                NotifyModuleToggle(host.Owner, uid, vision.Enabled);
                args.Handled = true;
                return;
            }

            if (TryComp<FishIpcModulePowerToggleComponent>(uid, out var power) &&
                power.ToggleActionEntity == action)
            {
                SetModulePower((uid, power), host.Owner, !power.Enabled);
                NotifyModuleToggle(host.Owner, uid, power.Enabled);
                args.Handled = true;
                return;
            }
        }
    }

    private void NotifyModuleToggle(EntityUid host, EntityUid module, bool enabled)
    {
        var key = enabled ? "fish-ipc-module-toggled-on" : "fish-ipc-module-toggled-off-named";
        _popup.PopupEntity(Loc.GetString(key, ("module", Name(module))), host, host);
    }

    private void SetModulePower(Entity<FishIpcModulePowerToggleComponent> module, EntityUid host, bool enabled)
    {
        module.Comp.Enabled = enabled;
        Dirty(module);
        if (module.Comp.ToggleActionEntity is { } action)
            _actions.SetToggled(action, enabled);

        SyncModuleEffects(module.Owner, host);

        var ev = new FishIpcModulePowerToggledEvent(host, enabled);
        RaiseLocalEvent(module.Owner, ref ev);
    }

    private void OnChassisPower(Entity<FishIpcModuleHostComponent> host, ref FishIpcChassisPowerChangedEvent args)
    {
        if (host.Comp.ModuleContainer is not { } container)
            return;

        foreach (var module in container.ContainedEntities)
            SyncModuleEffects(module, host.Owner);
    }

    /// <summary>
    /// Эффект есть только если модуль установлен, тумблер включён и шасси запитано.
    /// Тумблер при разряде не сбрасывается: заряд вернулся — эффект возвращается.
    /// </summary>
    private void SyncModuleEffects(EntityUid module, EntityUid host)
    {
        var active = IsModuleRunning(module, host);

        if (HasComp<FishIpcEmpScreenModuleComponent>(module))
            SetHostComp<FishIpcEmpScreenHostComponent>(host, active);

        if (HasComp<FishIpcRadFilterModuleComponent>(module))
            SetHostComp<FishIpcRadFilterHostComponent>(host, active);

        if (HasComp<FishIpcWireInterfaceModuleComponent>(module))
            SetHostComp<FishIpcWireInterfaceHostComponent>(host, active);

        if (HasComp<FishIpcVocalModModuleComponent>(module))
        {
            SetHostComp<FishIpcVocalModHostComponent>(host, active);
            if (TryComp<FishIpcComponent>(host, out var ipc))
                _radio.SetRadioEnabled((host, ipc), !ipc.PowerDown);
        }

        if (HasComp<FishIpcApcAdapterModuleComponent>(module))
            SyncApc(module, host, active);

        if (TryComp<FishIpcVisionModuleComponent>(module, out var vision))
            ApplyVision(host, vision.Kind, active);
    }

    private bool IsModuleRunning(EntityUid module, EntityUid host)
    {
        if (!TryComp<FishIpcModuleComponent>(module, out var installed) || !installed.Installed)
            return false;

        if (!_modules.IsChassisPowered(host))
            return false;

        if (TryComp<FishIpcModulePowerToggleComponent>(module, out var power) && !power.Enabled)
            return false;

        if (TryComp<FishIpcPassiveHealModuleComponent>(module, out var heal) && !heal.Enabled)
            return false;

        if (TryComp<FishIpcVisionModuleComponent>(module, out var vision) && !vision.Enabled)
            return false;

        return true;
    }

    private void SetHostComp<T>(EntityUid host, bool active) where T : Component, new()
    {
        if (active)
            EnsureComp<T>(host);
        else
            RemComp<T>(host);
    }

    private void SyncApc(EntityUid module, EntityUid host, bool active)
    {
        if (!TryComp<FishIpcModuleComponent>(module, out var installed) || !installed.Installed)
        {
            RestoreApcSpeed(host);
            RemComp<FishIpcApcAdapterHostComponent>(host);
            return;
        }

        if (!TryComp<FishBatteryDrinkerComponent>(host, out var drinker))
            return;

        var apc = EnsureComp<FishIpcApcAdapterHostComponent>(host);
        // Первый захват — базовая скорость, до множителя адаптера.
        apc.StoredDrinkSpeed ??= drinker.DrinkSpeed;
        var speed = apc.StoredDrinkSpeed.Value;
        drinker.DrinkSpeed = active ? speed * apc.DrinkSpeedFactor : speed;
        Dirty(host, drinker);
    }

    private void RestoreApcSpeed(EntityUid host)
    {
        if (!TryComp<FishIpcApcAdapterHostComponent>(host, out var apc) ||
            !TryComp<FishBatteryDrinkerComponent>(host, out var drinker) ||
            apc.StoredDrinkSpeed is not { } speed)
            return;

        drinker.DrinkSpeed = speed;
        Dirty(host, drinker);
    }

    private void ApplyVision(EntityUid host, FishIpcVisionKind kind, bool add)
    {
        switch (kind)
        {
            case FishIpcVisionKind.Medical:
                ToggleComp<ShowHealthBarsComponent>(host, add);
                ToggleComp<ShowHealthIconsComponent>(host, add);
                break;
            case FishIpcVisionKind.Security:
                ToggleComp<ShowJobIconsComponent>(host, add);
                ToggleComp<ShowMindShieldIconsComponent>(host, add);
                ToggleComp<ShowCriminalRecordIconsComponent>(host, add);
                break;
            case FishIpcVisionKind.Meson:
                // Защита глаз от сварки только пока модуль включён. Вспышки мезон не закрывает.
                ToggleComp<EyeProtectionComponent>(host, add);
                break;
            case FishIpcVisionKind.Thermal:
                if (add)
                {
                    var tv = EnsureComp<FishThermalVisionComponent>(host);
                    Dirty(host, tv);
                }
                else
                {
                    RemComp<FishThermalVisionComponent>(host);
                }
                break;
        }
    }

    private void ToggleComp<T>(EntityUid uid, bool add) where T : Component, new()
    {
        if (add)
            EnsureComp<T>(uid);
        else
            RemComp<T>(uid);
    }

    private void OnRadModify(Entity<FishIpcRadFilterHostComponent> ent, ref DamageModifyEvent args)
    {
        var modifiers = new DamageModifierSet
        {
            Coefficients =
            {
                ["Radiation"] = ent.Comp.Coefficient,
                ["Cellular"] = ent.Comp.Coefficient,
                ["Poison"] = MathF.Min(1f, ent.Comp.Coefficient + 0.25f),
            }
        };

        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifiers, args.ArmorPenetration, args.CanHeal);
    }

    private void OnWireExamineHost(Entity<FishIpcWireInterfaceHostComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("fish-ipc-module-wire-interface-self"));
    }
}
