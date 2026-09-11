using Content.Shared._Fish.PlanetWar;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Fish.PlanetWar;

/// <summary>
/// Отображение фракционных иконок PlanetWar.
/// </summary>
public sealed class PlanetWarMemberSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlanetWarMemberComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStatusIcons(Entity<PlanetWarMemberComponent> ent, ref GetStatusIconsEvent args)
    {
        args.StatusIcons.Add(_prototype.Index(ent.Comp.StatusIcon));
    }
}
