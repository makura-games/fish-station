using Content.Shared.Research.Components;
using Robust.Client.GameObjects;

namespace Content.Client.Research;

/// <summary>
/// Refreshes open research consoles after their networked technology database has been applied.
/// </summary>
public sealed partial class ResearchSystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TechnologyDatabaseComponent, AfterAutoHandleStateEvent>(OnFishDatabaseState);
    }

    private void OnFishDatabaseState(Entity<TechnologyDatabaseComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_ui.TryGetOpenUi(ent.Owner, ResearchConsoleUiKey.Key, out var bui))
            bui.Update<ResearchConsoleBoundInterfaceState>();
    }
}
