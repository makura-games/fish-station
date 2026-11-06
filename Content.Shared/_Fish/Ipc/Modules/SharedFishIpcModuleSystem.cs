using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Content.Shared.Wires;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Shared._Fish.Ipc.Modules;

/// <summary>
/// Общая логика установки/снятия модулей КПБ (панель должна быть открыта).
/// </summary>
public abstract partial class SharedFishIpcModuleSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishIpcModuleHostComponent, ComponentStartup>(OnHostStartup);
        SubscribeLocalEvent<FishIpcModuleHostComponent, AfterInteractUsingEvent>(OnHostInteractUsing);
        SubscribeLocalEvent<FishIpcModuleHostComponent, EntInsertedIntoContainerMessage>(OnModuleInserted);
        SubscribeLocalEvent<FishIpcModuleHostComponent, EntRemovedFromContainerMessage>(OnModuleRemoved);
        SubscribeLocalEvent<FishIpcModuleHostComponent, GetVerbsEvent<AlternativeVerb>>(OnHostVerbs);
    }

    private void OnHostVerbs(Entity<FishIpcModuleHostComponent> host, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        if (_lock.IsLocked(host.Owner))
            return;

        if (TryComp<WiresPanelComponent>(host, out var panel) && !panel.Open)
            return;

        var user = args.User;
        var priority = 0;
        foreach (var moduleUid in host.Comp.ModuleContainer.ContainedEntities)
        {
            var module = moduleUid;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("fish-ipc-module-eject", ("module", module)),
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
                Priority = priority--,
                Act = () =>
                {
                    if (!_container.Remove(module, host.Comp.ModuleContainer))
                        return;

                    _hands.PickupOrDrop(user, module);
                },
            });
        }
    }

    private void OnHostStartup(Entity<FishIpcModuleHostComponent> host, ref ComponentStartup args)
    {
        host.Comp.ModuleContainer = _container.EnsureContainer<Container>(host.Owner, host.Comp.ModuleContainerId);
    }

    private void OnHostInteractUsing(Entity<FishIpcModuleHostComponent> host, ref AfterInteractUsingEvent args)
    {
        if (!args.CanReach || args.Handled || host.Owner == args.User)
            return;

        if (!TryComp<FishIpcModuleComponent>(args.Used, out var module))
            return;

        if (TryComp<WiresPanelComponent>(host, out var panel) && !panel.Open)
        {
            _popup.PopupEntity(Loc.GetString("fish-ipc-module-panel-closed"), host, args.User);
            return;
        }

        if (_lock.IsLocked(host.Owner))
        {
            _popup.PopupEntity(Loc.GetString("fish-ipc-module-locked"), host, args.User);
            return;
        }

        if (!CanInsertModule(host, (args.Used, module), args.User))
            return;

        if (!_container.Insert(args.Used, host.Comp.ModuleContainer))
            return;

        args.Handled = true;
    }

    private void OnModuleInserted(Entity<FishIpcModuleHostComponent> host, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != host.Comp.ModuleContainerId)
            return;

        if (!TryComp<FishIpcModuleComponent>(args.Entity, out var module) || module.Installed)
            return;

        InstallModule(host, (args.Entity, module));
    }

    private void OnModuleRemoved(Entity<FishIpcModuleHostComponent> host, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != host.Comp.ModuleContainerId)
            return;

        if (!TryComp<FishIpcModuleComponent>(args.Entity, out var module) || !module.Installed)
            return;

        UninstallModule(host, (args.Entity, module));
    }

    public bool CanInsertModule(Entity<FishIpcModuleHostComponent> host, Entity<FishIpcModuleComponent> module, EntityUid? user = null)
    {
        if (host.Comp.ModuleContainer.ContainedEntities.Count >= host.Comp.MaxModules)
        {
            _popup.PopupEntity(Loc.GetString("fish-ipc-module-too-many", ("max", host.Comp.MaxModules)), host, user);
            return false;
        }

        if (_whitelist.IsWhitelistFail(host.Comp.ModuleWhitelist, module))
        {
            _popup.PopupEntity(Loc.GetString("fish-ipc-module-whitelist-deny"), host, user);
            return false;
        }

        if (!string.IsNullOrEmpty(module.Comp.ExclusionGroup))
        {
            foreach (var other in host.Comp.ModuleContainer.ContainedEntities)
            {
                if (!TryComp<FishIpcModuleComponent>(other, out var otherMod))
                    continue;

                if (otherMod.ExclusionGroup == module.Comp.ExclusionGroup)
                {
                    _popup.PopupEntity(Loc.GetString("fish-ipc-module-exclusion"), host, user);
                    return false;
                }
            }
        }

        var attempt = new FishIpcModuleInsertAttemptEvent(module.Owner, host.Owner);
        RaiseLocalEvent(host.Owner, ref attempt);
        RaiseLocalEvent(module.Owner, ref attempt);
        if (attempt.Cancelled)
            return false;

        return true;
    }

    public void InstallModule(Entity<FishIpcModuleHostComponent> host, Entity<FishIpcModuleComponent> module)
    {
        if (module.Comp.Installed)
            return;

        module.Comp.InstalledEntity = host.Owner;
        Dirty(module);
        var ev = new FishIpcModuleInstalledEvent(host.Owner);
        RaiseLocalEvent(module.Owner, ref ev);
    }

    public void UninstallModule(Entity<FishIpcModuleHostComponent> host, Entity<FishIpcModuleComponent> module)
    {
        if (!module.Comp.Installed)
            return;

        var hostUid = module.Comp.InstalledEntity ?? host.Owner;
        module.Comp.InstalledEntity = null;
        Dirty(module);
        var ev = new FishIpcModuleUninstalledEvent(hostUid);
        RaiseLocalEvent(module.Owner, ref ev);
    }
}
