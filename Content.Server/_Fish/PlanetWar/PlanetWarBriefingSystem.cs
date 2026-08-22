using Content.Server.Antag;
using Content.Shared._Fish.PlanetWar;
using Content.Shared.Mind.Components;
using Robust.Shared.Player;

namespace Content.Server._Fish.PlanetWar;

/// <summary>
/// Показывает настраиваемый брифинг при добавлении разума (гост-роль PlanetWar).
/// </summary>
public sealed class PlanetWarBriefingSystem : EntitySystem
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlanetWarBriefingComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnMindAdded(EntityUid uid, PlanetWarBriefingComponent component, MindAddedMessage args)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        _antag.SendBriefing(
            actor.PlayerSession,
            Loc.GetString(component.BriefingText),
            component.BriefingColor,
            component.BriefingSound);
    }
}
