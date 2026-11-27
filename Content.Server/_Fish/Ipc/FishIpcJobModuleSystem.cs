using System.Linq;
using Content.Server._Fish.Ipc.Modules;
using Content.Server.Antag;
using Content.Shared._Fish.Ipc;
using Content.Shared._Fish.Ipc.Modules;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GameTicking;
using Content.Shared.PowerCell.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Ipc;

/// <summary>
/// Стартовые модули шасси: ОБР/NukeOps по роли (EmpScreen + профа), станция — HUD отдела.
/// Боевым ролям — PowerCellSyndicate и запечатанный слот.
/// </summary>
public sealed partial class FishIpcJobModuleSystem : EntitySystem
{
    private static readonly EntProtoId SecHudModule = "FishIpcModuleSecHud";
    private static readonly EntProtoId MedHudModule = "FishIpcModuleMedHud";

    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private FishIpcModuleSystem _modules = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private Dictionary<string, FishIpcRoleModuleLoadoutPrototype> _loadoutsByRole = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishIpcComponent, PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
        SubscribeLocalEvent<AfterAntagEntitySelectedEvent>(OnAntagSelected);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        RebuildLoadoutCache();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<FishIpcRoleModuleLoadoutPrototype>())
            RebuildLoadoutCache();
    }

    private void RebuildLoadoutCache()
    {
        _loadoutsByRole = _prototypes.EnumeratePrototypes<FishIpcRoleModuleLoadoutPrototype>()
            .ToDictionary(static p => p.Role, static p => p);
    }

    private void OnSpawnComplete(Entity<FishIpcComponent> ent, ref PlayerSpawnCompleteEvent args)
    {
        if (args.Mob != ent.Owner || args.JobId is not { } jobId)
            return;

        TryGrantForRole(ent.Owner, jobId);
    }

    private void OnRoleAdded(RoleAddedEvent args)
    {
        if (args.Mind.OwnedEntity is not { } body || !HasComp<FishIpcComponent>(body))
            return;

        if (!_jobs.MindTryGetJobId(args.MindId, out var jobId) || jobId is null)
            return;

        TryGrantForRole(body, jobId);
    }

    private void OnAntagSelected(ref AfterAntagEntitySelectedEvent args)
    {
        if (!HasComp<FishIpcComponent>(args.EntityUid))
            return;

        TryGrantForRole(args.EntityUid, args.Def.ID);
    }

    /// <summary>
    /// Выдаёт loadout по role id; иначе Sec/Med HUD по отделу станции.
    /// </summary>
    public void TryGrantForRole(EntityUid ipc, string roleId)
    {
        if (_loadoutsByRole.TryGetValue(roleId, out var loadout))
        {
            GrantModules(ipc, loadout.Modules);
            if (loadout.PowerCell is { } cell)
                EnsureCombatBattery(ipc, cell, loadout.SealPowerCell);
            return;
        }

        if (!_jobs.TryGetDepartment(roleId, out var department))
            return;

        EntProtoId moduleId;
        switch (department.ID)
        {
            case "Security":
                moduleId = SecHudModule;
                break;
            case "Medical":
                moduleId = MedHudModule;
                break;
            default:
                return;
        }

        GrantModules(ipc, [moduleId]);
    }

    private void EnsureCombatBattery(EntityUid ipc, EntProtoId cellProto, bool seal)
    {
        if (!TryComp<PowerCellSlotComponent>(ipc, out var cellSlot) ||
            !_itemSlots.TryGetSlot(ipc, cellSlot.CellSlotId, out var slot) ||
            slot.ContainerSlot is not { } container)
            return;

        // Уже стоит нужный прототип — только печать.
        if (slot.Item is { } existing &&
            MetaData(existing).EntityPrototype?.ID == cellProto.Id)
        {
            if (seal)
                EnsureComp<FishIpcSealedBatteryComponent>(ipc);
            return;
        }

        // Сразу убираем старую ячейку: QueueDel оставит её в слоте до конца тика.
        foreach (var old in container.ContainedEntities.ToArray())
        {
            _container.Remove(old, container, force: true);
            Del(old);
        }

        var cell = Spawn(cellProto);
        if (!_container.Insert(cell, container))
        {
            Del(cell);
            return;
        }

        if (seal)
            EnsureComp<FishIpcSealedBatteryComponent>(ipc);
    }

    private void GrantModules(EntityUid ipc, IReadOnlyList<EntProtoId> moduleIds)
    {
        if (!TryComp<FishIpcModuleHostComponent>(ipc, out var host))
            return;

        var hostEnt = new Entity<FishIpcModuleHostComponent>(ipc, host);
        if (host.ModuleContainer is not { } container)
            return;

        var existingProtos = new HashSet<string>();
        foreach (var existing in container.ContainedEntities)
        {
            if (MetaData(existing).EntityPrototype is { } proto)
                existingProtos.Add(proto.ID);
        }

        foreach (var moduleId in moduleIds)
        {
            if (existingProtos.Contains(moduleId.Id))
                continue;

            var module = Spawn(moduleId);
            if (!TryComp<FishIpcModuleComponent>(module, out var moduleComp))
            {
                QueueDel(module);
                continue;
            }

            // Уже стоит оптика той же exclusion group — не дублируем vision-слот.
            if (moduleComp.ExclusionGroup is { } group)
            {
                var conflict = false;
                foreach (var existing in container.ContainedEntities)
                {
                    if (TryComp<FishIpcModuleComponent>(existing, out var mod) &&
                        mod.ExclusionGroup == group)
                    {
                        conflict = true;
                        break;
                    }
                }

                if (conflict)
                {
                    QueueDel(module);
                    continue;
                }
            }

            if (!_modules.CanInsertModule(hostEnt, (module, moduleComp)))
            {
                QueueDel(module);
                continue;
            }

            if (!_container.Insert(module, container))
            {
                QueueDel(module);
                continue;
            }

            existingProtos.Add(moduleId.Id);
        }
    }
}
