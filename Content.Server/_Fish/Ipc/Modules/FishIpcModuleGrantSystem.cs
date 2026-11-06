using Content.Shared._Fish.Ipc;
using Content.Shared._Fish.Ipc.Modules;
using Content.Shared._Fish.Vision;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Overlays;

namespace Content.Server._Fish.Ipc.Modules;

/// <summary>
/// Установка/снятие маркеров хоста для пассивных модулей (HUD, фильтры, адаптер, вокал, провода).
/// </summary>
public sealed partial class FishIpcModuleGrantSystem : EntitySystem
{
    [Dependency] private FishIpcRadioSystem _radio = default!;

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

        SubscribeLocalEvent<FishIpcRadFilterHostComponent, DamageModifyEvent>(OnRadModify);
        SubscribeLocalEvent<FishIpcWireInterfaceHostComponent, ExaminedEvent>(OnWireExamineHost);
        // Examine панелей — Fish-edit в SharedWiresSystem (одна подписка Comp+Event на весь процесс).
    }

    private void OnEmpScreenInstall(Entity<FishIpcEmpScreenModuleComponent> ent, ref FishIpcModuleInstalledEvent args) =>
        EnsureComp<FishIpcEmpScreenHostComponent>(args.Host);

    private void OnEmpScreenUninstall(Entity<FishIpcEmpScreenModuleComponent> ent, ref FishIpcModuleUninstalledEvent args) =>
        RemComp<FishIpcEmpScreenHostComponent>(args.Host);

    private void OnMagInstall(Entity<FishIpcMagneticJointsModuleComponent> ent, ref FishIpcModuleInstalledEvent args) =>
        EnsureComp<FishIpcMagneticJointsHostComponent>(args.Host);

    private void OnMagUninstall(Entity<FishIpcMagneticJointsModuleComponent> ent, ref FishIpcModuleUninstalledEvent args) =>
        RemComp<FishIpcMagneticJointsHostComponent>(args.Host);

    private void OnSealInstall(Entity<FishIpcSealedJointsModuleComponent> ent, ref FishIpcModuleInstalledEvent args) =>
        EnsureComp<FishIpcSealedJointsHostComponent>(args.Host);

    private void OnSealUninstall(Entity<FishIpcSealedJointsModuleComponent> ent, ref FishIpcModuleUninstalledEvent args) =>
        RemComp<FishIpcSealedJointsHostComponent>(args.Host);

    private void OnApcInstall(Entity<FishIpcApcAdapterModuleComponent> ent, ref FishIpcModuleInstalledEvent args)
    {
        var host = EnsureComp<FishIpcApcAdapterHostComponent>(args.Host);
        if (!TryComp<FishBatteryDrinkerComponent>(args.Host, out var drinker))
            return;

        host.StoredDrinkMultiplier ??= drinker.DrinkMultiplier;
        host.StoredDrinkSpeed ??= drinker.DrinkSpeed;
        drinker.DrinkMultiplier = host.StoredDrinkMultiplier.Value * host.DrinkMultiplierBonus;
        drinker.DrinkSpeed = host.StoredDrinkSpeed.Value * host.DrinkSpeedFactor;
        Dirty(args.Host, drinker);
    }

    private void OnApcUninstall(Entity<FishIpcApcAdapterModuleComponent> ent, ref FishIpcModuleUninstalledEvent args)
    {
        if (TryComp<FishIpcApcAdapterHostComponent>(args.Host, out var host) &&
            TryComp<FishBatteryDrinkerComponent>(args.Host, out var drinker))
        {
            if (host.StoredDrinkMultiplier is { } mult)
                drinker.DrinkMultiplier = mult;
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
        // Если шасси уже в power-down — сразу поднять рацию через SetRadioEnabled.
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

    private void OnVisionInstall(Entity<FishIpcVisionModuleComponent> ent, ref FishIpcModuleInstalledEvent args) =>
        ApplyVision(args.Host, ent.Comp.Kind, add: true);

    private void OnVisionUninstall(Entity<FishIpcVisionModuleComponent> ent, ref FishIpcModuleUninstalledEvent args) =>
        ApplyVision(args.Host, ent.Comp.Kind, add: false);

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
                ToggleComp<EyeProtectionComponent>(host, add);
                break;
            case FishIpcVisionKind.NightVision:
                // Fish-оптика: без toggle-action и с клиентским FishVisionSystem.
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
