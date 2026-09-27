using Content.Client.Guidebook;
using Content.Shared._Fish.PlanetWar;
using Robust.Shared.Timing;

namespace Content.Client._Fish.PlanetWar;

public sealed partial class PlanetWarGuideSystem : EntitySystem
{
    [Dependency] private GuidebookSystem _guidebookSystem = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OpenPlanetWarGuideActionEvent>(OnOpenPlanetWarGuide);
    }

    private void OnOpenPlanetWarGuide(OpenPlanetWarGuideActionEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        _guidebookSystem.OpenHelp(new() { "PlanetWar" });
        args.Handled = true;
    }
}
