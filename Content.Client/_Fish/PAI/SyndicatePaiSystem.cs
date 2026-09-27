using Content.Shared._Fish.PAI;
using Content.Shared.Mind;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._Fish.PAI;

public sealed partial class SyndicatePaiSystem : SharedSyndicatePaiSystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SyndicatePaiComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<SyndicatePaiComponent, AppearanceChangeEvent>(OnAppearance);
    }

    private void OnAfterState(Entity<SyndicatePaiComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateDisguiseVisuals(ent);
    }

    private void OnAppearance(Entity<SyndicatePaiComponent> ent, ref AppearanceChangeEvent args)
    {
        UpdateDisguiseVisuals(ent);
    }

    /// <summary>
    /// Подменяет оверлей экрана на обычный пИИ при активной маскировке.
    /// </summary>
    private void UpdateDisguiseVisuals(Entity<SyndicatePaiComponent> ent)
    {
        if (!ent.Comp.Disguised)
            return;

        if (!TryComp(ent.Owner, out SpriteComponent? sprite))
            return;

        if (!_appearance.TryGetData(ent.Owner, ToggleableGhostRoleVisuals.Status, out ToggleableGhostRoleStatus status))
            status = ToggleableGhostRoleStatus.Off;

        var state = status switch
        {
            ToggleableGhostRoleStatus.On => "pai-on-overlay",
            ToggleableGhostRoleStatus.Searching => "pai-searching-overlay",
            _ => "pai-off-overlay",
        };

        _sprite.LayerSetRsiState((ent.Owner, sprite), "screen", state);
    }
}
