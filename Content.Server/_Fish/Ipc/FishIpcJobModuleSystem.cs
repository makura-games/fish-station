using Content.Server._Fish.Ipc.Modules;
using Content.Shared._Fish.Ipc;
using Content.Shared._Fish.Ipc.Modules;
using Content.Shared.GameTicking;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Ipc;

/// <summary>
/// Стартовые модули шасси по отделу: СБ → сек-HUD, мед → мед-HUD.
/// </summary>
public sealed partial class FishIpcJobModuleSystem : EntitySystem
{
    private static readonly EntProtoId SecHudModule = "FishIpcModuleSecHud";
    private static readonly EntProtoId MedHudModule = "FishIpcModuleMedHud";

    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private FishIpcModuleSystem _modules = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishIpcComponent, PlayerSpawnCompleteEvent>(OnSpawnComplete);
    }

    private void OnSpawnComplete(Entity<FishIpcComponent> ent, ref PlayerSpawnCompleteEvent args)
    {
        if (args.Mob != ent.Owner || args.JobId is not { } jobId)
            return;

        if (!_jobs.TryGetDepartment(jobId, out var department))
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

        if (!TryComp<FishIpcModuleHostComponent>(ent.Owner, out var host))
            return;

        var hostEnt = new Entity<FishIpcModuleHostComponent>(ent.Owner, host);
        if (host.ModuleContainer is not { } container)
            return;

        // Уже стоит оптика этой же группы — не дублируем.
        foreach (var existing in container.ContainedEntities)
        {
            if (TryComp<FishIpcModuleComponent>(existing, out var mod) &&
                mod.ExclusionGroup == "vision")
                return;
        }

        var module = Spawn(moduleId);
        if (!TryComp<FishIpcModuleComponent>(module, out var moduleComp))
        {
            QueueDel(module);
            return;
        }

        if (!_modules.CanInsertModule(hostEnt, (module, moduleComp)))
        {
            QueueDel(module);
            return;
        }

        _container.Insert(module, container);
    }
}
