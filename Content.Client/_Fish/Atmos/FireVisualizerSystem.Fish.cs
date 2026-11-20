using Content.Client.Atmos.Components;
using Robust.Client.GameObjects;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.Atmos.EntitySystems;

public sealed partial class FireVisualizerSystem
{
    private void OnComponentStartup(Entity<FireVisualsComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) || !TryComp(ent, out AppearanceComponent? appearance))
            return;

        UpdateAppearance(ent, ent.Comp, sprite, appearance);
    }
}
