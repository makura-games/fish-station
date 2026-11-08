using Content.Shared._Fish.Ipc;
using Content.Shared._Fish.Ipc.Modules;
using Content.Shared._Fish.Vision;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Overlays;

namespace Content.Server._Fish.Ipc.Modules;

/// <summary>
/// Установка/снятие маркеров хоста и toggle-actions пассивных модулей.
/// </summary>
public sealed partial class FishIpcModuleGrantSystem : EntitySystem
{
    [Dependency] private FishIpcRadioSystem _radio = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MetaDataSystem _meta = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishIpcEmpScreenModuleComponent, FishIpcModuleInstalledEvent>(OnEmpScreenInstall);
        SubscribeLocalEvent<FishIpcEmpScreenModuleComponent, FishIpcModuleUninstalledEvent>(OnEmpScreenUninstall);

        SubscribeLocalEvent<FishIpcMagneticJointsModuleComponent, FishIpcModuleInstalledEvent>(OnMagInstall);
        SubscribeLocalEvent<FishIpcMagneticJointsModuleComponent, FishIpcModuleUninstalledEvent>(OnMagUninstall);

        SubscribeLocalEvent<FishIpcSealedJointsModuleComponent, FishIpcModuleInstalledEvent>(OnSealInstall);
        SubscribeLocalEvent<FishIpcSealedJointsModuleComponent, FishIpcModuleUninstalledEvent>(OnSealUninstall);

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

        SubscribeLocalEvent<FishIpcRadFilterHostComponent, DamageModifyEvent>(OnRadModify);
        SubscribeLocalEvent<FishIpcWireInterfaceHostComponent, ExaminedEvent>(OnWireExamineHost);
    }

    private void OnEmpScreenInstall(Entity<FishIpcEmpScreenModuleComponent> ent, ref FishIpcModuleInstalledEvent args) =>
        EnsureComp<FishIpcEmpScreenHostComponent>(args.Host);

    private void OnEmpScreenUninstall(Entity<FishIpcEmpScreenModuleComponent> ent, ref FishIpcModuleUninstalledEvent args) =>
        RemComp<FishIpcEmpScreenHostComponent>(args.Host);

    private void OnMagInstall(Entity<FishIpcMagneticJointsModuleComponent> ent, ref FishIpcModuleInstalledEvent args)
    {
        var host = EnsureComp<FishIpcMagneticJointsHostComponent>(args.Host);
        host.ModuleEnabled = ent.Comp.Enabled;
        Dirty(args.Host, host);
        _actions.AddAction(args.Host, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
        if (ent.Comp.ToggleActionEntity is { } action)
            _actions.SetToggled(action, ent.Comp.Enabled);
    }

    private void OnMagUninstall(Entity<FishIpcMagneticJointsModuleComponent> ent, ref FishIpcModuleUninstalledEvent args)
    {
        _actions.RemoveAction(args.Host, ent.Comp.ToggleActionEntity);
        ent.Comp.ToggleActionEntity = null;
        RemComp<FishIpcMagneticJointsHostComponent>(args.Host);
    }

    private void OnSealInstall(Entity<FishIpcSealedJointsModuleComponent> ent, ref FishIpcModuleInstalledEvent args)
    {
        var host = EnsureComp<FishIpcSealedJointsHostComponent>(args.Host);
        host.ModuleEnabled = ent.Comp.Enabled;
        Dirty(args.Host, host);
        _actions.AddAction(args.Host, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
        if (ent.Comp.ToggleActionEntity is { } action)
            _actions.SetToggled(action, ent.Comp.Enabled);
    }

    private void OnSealUninstall(Entity<FishIpcSealedJointsModuleComponent> ent, ref FishIpcModuleUninstalledEvent args)
    {
        _actions.RemoveAction(args.Host, ent.Comp.ToggleActionEntity);
        ent.Comp.ToggleActionEntity = null;
        RemComp<FishIpcSealedJointsHostComponent>(args.Host);
    }

    private void OnApcInstall(Entity<FishIpcApcAdapterModuleComponent> ent, ref FishIpcModuleInstalledEvent args)
    {
        var host = EnsureComp<FishIpcApcAdapterHostComponent>(args.Host);
        if (!TryComp<FishBatteryDrinkerComponent>(args.Host, out var drinker))
            return;

        // Адаптер ускоряет do-after, порция остаётся 5% за цикл.
        host.StoredDrinkSpeed ??= drinker.DrinkSpeed;
        drinker.DrinkSpeed = host.StoredDrinkSpeed.Value * host.DrinkSpeedFactor;
        Dirty(args.Host, drinker);
    }

    private void OnApcUninstall(Entity<FishIpcApcAdapterModuleComponent> ent, ref FishIpcModuleUninstalledEvent args)
    {
        if (TryComp<FishIpcApcAdapterHostComponent>(args.Host, out var host) &&
            TryComp<FishBatteryDrinkerComponent>(args.Host, out var drinker))
        {
            if (host.StoredDrinkSpeed is { } speed)
                drinker.DrinkSpeed = speed;
            Dirty(args.Host, drinker);
        }

        RemComp<FishIpcApcAdapterHostComponent>(args.Host);
    }

    private void OnRadInstall(Entity<FishIpcRadFilterModuleComponent> ent, ref FishIpcModuleInstalledEvent args) =>
        EnsureComp<FishIpcRadFilterHostComponent>(args.Host);

    private void OnRadUninstall(Entity<FishIpcRadFilterModuleComponent> ent, ref FishIpcModuleUninstalledEvent args) =>
        RemComp<FishIpcRadFilterHostComponent>(args.Host);

    private void OnVocalInstall(Entity<FishIpcVocalModModuleComponent> ent, ref FishIpcModuleInstalledEvent args)
    {
        EnsureComp<FishIpcVocalModHostComponent>(args.Host);
        if (TryComp<FishIpcComponent>(args.Host, out var ipc))
            _radio.SetRadioEnabled((args.Host, ipc), !ipc.PowerDown);
    }

    private void OnVocalUninstall(Entity<FishIpcVocalModModuleComponent> ent, ref FishIpcModuleUninstalledEvent args)
    {
        RemComp<FishIpcVocalModHostComponent>(args.Host);
        if (TryComp<FishIpcComponent>(args.Host, out var ipc))
            _radio.SetRadioEnabled((args.Host, ipc), !ipc.PowerDown);
    }

    private void OnWireInstall(Entity<FishIpcWireInterfaceModuleComponent> ent, ref FishIpcModuleInstalledEvent args) =>
        EnsureComp<FishIpcWireInterfaceHostComponent>(args.Host);

    private void OnWireUninstall(Entity<FishIpcWireInterfaceModuleComponent> ent, ref FishIpcModuleUninstalledEvent args) =>
        RemComp<FishIpcWireInterfaceHostComponent>(args.Host);

    private void OnVisionInstall(Entity<FishIpcVisionModuleComponent> ent, ref FishIpcModuleInstalledEvent args)
    {
        if (ent.Comp.Enabled)
            ApplyVision(args.Host, ent.Comp.Kind, add: true);

        _actions.AddAction(args.Host, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
        if (ent.Comp.ToggleActionEntity is { } action)
            _actions.SetToggled(action, ent.Comp.Enabled);
    }

    private void OnVisionUninstall(Entity<FishIpcVisionModuleComponent> ent, ref FishIpcModuleUninstalledEvent args)
    {
        if (ent.Comp.Enabled)
            ApplyVision(args.Host, ent.Comp.Kind, add: false);

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
                args.Handled = true;
                return;
            }

            if (TryComp<FishIpcVisionModuleComponent>(uid, out var vision) &&
                vision.ToggleActionEntity == action)
            {
                vision.Enabled = !vision.Enabled;
                Dirty(uid, vision);
                ApplyVision(host.Owner, vision.Kind, vision.Enabled);
                _actions.SetToggled(action, vision.Enabled);
                args.Handled = true;
                return;
            }

            if (TryComp<FishIpcMagneticJointsModuleComponent>(uid, out var mag) &&
                mag.ToggleActionEntity == action)
            {
                mag.Enabled = !mag.Enabled;
                Dirty(uid, mag);
                if (TryComp<FishIpcMagneticJointsHostComponent>(host.Owner, out var magHost))
                {
                    magHost.ModuleEnabled = mag.Enabled;
                    Dirty(host.Owner, magHost);
                }

                _actions.SetToggled(action, mag.Enabled);
                args.Handled = true;
                return;
            }

            if (TryComp<FishIpcSealedJointsModuleComponent>(uid, out var seal) &&
                seal.ToggleActionEntity == action)
            {
                seal.Enabled = !seal.Enabled;
                Dirty(uid, seal);
                if (TryComp<FishIpcSealedJointsHostComponent>(host.Owner, out var sealHost))
                {
                    sealHost.ModuleEnabled = seal.Enabled;
                    Dirty(host.Owner, sealHost);
                }

                _actions.SetToggled(action, seal.Enabled);
                args.Handled = true;
                return;
            }

            if (TryComp<FishIpcModulePowerToggleComponent>(uid, out var power) &&
                power.ToggleActionEntity == action)
            {
                SetModulePower((uid, power), host.Owner, !power.Enabled);
                args.Handled = true;
                return;
            }
        }
    }

    private void SetModulePower(Entity<FishIpcModulePowerToggleComponent> module, EntityUid host, bool enabled)
    {
        module.Comp.Enabled = enabled;
        Dirty(module);
        if (module.Comp.ToggleActionEntity is { } action)
            _actions.SetToggled(action, enabled);

        ApplyPowerEffects(module.Owner, host, enabled);
    }

    /// <summary>Включает или снимает эффект модулей, у которых эффект — это компонент хоста.</summary>
    private void ApplyPowerEffects(EntityUid module, EntityUid host, bool enabled)
    {
        if (HasComp<FishIpcEmpScreenModuleComponent>(module))
        {
            if (enabled)
                EnsureComp<FishIpcEmpScreenHostComponent>(host);
            else
                RemComp<FishIpcEmpScreenHostComponent>(host);
        }

        if (HasComp<FishIpcRadFilterModuleComponent>(module))
        {
            if (enabled)
                EnsureComp<FishIpcRadFilterHostComponent>(host);
            else
                RemComp<FishIpcRadFilterHostComponent>(host);
        }

        if (HasComp<FishIpcWireInterfaceModuleComponent>(module))
        {
            if (enabled)
                EnsureComp<FishIpcWireInterfaceHostComponent>(host);
            else
                RemComp<FishIpcWireInterfaceHostComponent>(host);
        }

        if (HasComp<FishIpcVocalModModuleComponent>(module))
        {
            if (enabled)
                EnsureComp<FishIpcVocalModHostComponent>(host);
            else
                RemComp<FishIpcVocalModHostComponent>(host);

            if (TryComp<FishIpcComponent>(host, out var ipc))
                _radio.SetRadioEnabled((host, ipc), !ipc.PowerDown);
        }

        if (HasComp<FishIpcApcAdapterModuleComponent>(module) &&
            TryComp<FishIpcApcAdapterHostComponent>(host, out var apc) &&
            TryComp<FishBatteryDrinkerComponent>(host, out var drinker) &&
            apc.StoredDrinkSpeed is { } speed)
        {
            drinker.DrinkSpeed = enabled ? speed * apc.DrinkSpeedFactor : speed;
            Dirty(host, drinker);
        }

        var ev = new FishIpcModulePowerToggledEvent(host, enabled);
        RaiseLocalEvent(module, ref ev);
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
            case FishIpcVisionKind.NightVision:
                if (add)
                {
                    var nv = EnsureComp<FishNightVisionComponent>(host);
                    Dirty(host, nv);
                }
                else
                {
                    RemComp<FishNightVisionComponent>(host);
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
