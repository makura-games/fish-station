using System.Linq;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Content.Shared.Wires;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
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
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    // У боргов отдельного звука установки модуля нет — тот же щелчок, что у слота карты.
    private static readonly SoundSpecifier ModuleInsertSound = new SoundPathSpecifier("/Audio/Machines/id_insert.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishIpcModuleHostComponent, ComponentStartup>(OnHostStartup);
        SubscribeLocalEvent<FishIpcModuleHostComponent, ComponentShutdown>(OnHostShutdown);
        SubscribeLocalEvent<FishIpcModuleHostComponent, AfterInteractUsingEvent>(OnHostInteractUsing);
        SubscribeLocalEvent<FishIpcModuleHostComponent, FishIpcModuleInsertDoAfterEvent>(OnModuleInsertDoAfter);
        SubscribeLocalEvent<FishIpcModuleHostComponent, EntInsertedIntoContainerMessage>(OnModuleInserted);
        SubscribeLocalEvent<FishIpcModuleHostComponent, EntRemovedFromContainerMessage>(OnModuleRemoved);
        SubscribeLocalEvent<FishIpcModuleHostComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<FishIpcModuleHostComponent, GetVerbsEvent<AlternativeVerb>>(OnHostVerbs);
    }

    private void OnHostShutdown(Entity<FishIpcModuleHostComponent> host, ref ComponentShutdown args)
    {
        if (host.Comp.ModuleContainer is not { } container)
            return;

        // Снимаем эффекты модулей до удаления контейнера.
        foreach (var moduleUid in container.ContainedEntities.ToArray())
        {
            if (TryComp<FishIpcModuleComponent>(moduleUid, out var module) && module.Installed)
                UninstallModule(host, (moduleUid, module));
        }
    }

    private void OnInsertAttempt(Entity<FishIpcModuleHostComponent> host, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID != host.Comp.ModuleContainerId)
            return;

        if (!TryComp<FishIpcModuleComponent>(args.EntityUid, out var module))
        {
            args.Cancel();
            return;
        }

        if (!CanInsertModule(host, (args.EntityUid, module)))
            args.Cancel();
    }

    private void OnHostVerbs(Entity<FishIpcModuleHostComponent> host, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        if (_lock.IsLocked(host.Owner))
            return;

        if (TryComp<WiresPanelComponent>(host, out var panel) && !panel.Open)
            return;

        if (host.Comp.ModuleContainer is not { } container)
            return;

        var user = args.User;
        var priority = 0;
        foreach (var moduleUid in container.ContainedEntities)
        {
            var module = moduleUid;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Name(module),
                // Как «Извлечь» — один выпадающий список, а не отдельный verb на каждый модуль.
                Category = VerbCategory.Eject,
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
                Priority = priority--,
                Act = () =>
                {
                    if (!_container.Remove(module, container))
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
        // Самоустановка разрешена: панель/замок всё равно проверяются ниже.
        if (!args.CanReach || args.Handled)
            return;

        if (!TryComp<FishIpcModuleComponent>(args.Used, out var module))
            return;

        // Это модуль — не отдаём interact другим системам.
        args.Handled = true;

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

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, 1f, new FishIpcModuleInsertDoAfterEvent(), host, host, args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DistanceThreshold = 1.5f,
        });
    }

    private void OnModuleInsertDoAfter(Entity<FishIpcModuleHostComponent> host, ref FishIpcModuleInsertDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used)
            return;

        if (!TryComp<FishIpcModuleComponent>(used, out var module))
            return;

        if (TryComp<WiresPanelComponent>(host, out var panel) && !panel.Open)
            return;

        if (_lock.IsLocked(host.Owner))
            return;

        if (!CanInsertModule(host, (used, module), args.User))
            return;

        if (host.Comp.ModuleContainer is not { } insertContainer)
            return;

        args.Handled = true;
        _container.Insert(used, insertContainer);
    }

    private void OnModuleInserted(Entity<FishIpcModuleHostComponent> host, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != host.Comp.ModuleContainerId)
            return;

        if (!TryComp<FishIpcModuleComponent>(args.Entity, out var module) || module.Installed)
            return;

        _audio.PlayPredicted(ModuleInsertSound, host, null);
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
        if (host.Comp.ModuleContainer is not { } container)
            return false;

        if (container.ContainedEntities.Count >= host.Comp.MaxModules)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("fish-ipc-module-too-many", ("max", host.Comp.MaxModules)), host, user.Value);
            return false;
        }

        if (_whitelist.IsWhitelistFail(host.Comp.ModuleWhitelist, module))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("fish-ipc-module-whitelist-deny"), host, user.Value);
            return false;
        }

        if (!string.IsNullOrEmpty(module.Comp.ExclusionGroup))
        {
            foreach (var other in container.ContainedEntities)
            {
                if (other == module.Owner)
                    continue;

                if (!TryComp<FishIpcModuleComponent>(other, out var otherMod))
                    continue;

                if (otherMod.ExclusionGroup == module.Comp.ExclusionGroup)
                {
                    if (user != null)
                        _popup.PopupEntity(Loc.GetString("fish-ipc-module-exclusion"), host, user.Value);
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
