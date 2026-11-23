using Content.Shared.Materials;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Materials;

public sealed partial class MaterialStorageSystem
{
    [Dependency] private IGameTiming _fishTiming = default!;

    private void OnFishIntakeState(Entity<InsertingMaterialStorageComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp<MaterialStorageComponent>(ent, out var storage) &&
            _sprite.LayerMapTryGet(ent.Owner, MaterialStorageVisualLayers.Inserting, out var index, false))
            TryShowFishIntake(ent.Owner, index, storage.InsertionTime, ent.Comp);
    }

    private bool TryShowFishIntake(Entity<SpriteComponent?> sprite, int index, TimeSpan duration,
        InsertingMaterialStorageComponent insertion)
    {
        if (!Resolve(sprite.Owner, ref sprite.Comp) ||
            !_sprite.TryGetLayer(sprite, index, out var layer, false) ||
            layer.ShaderPrototype != "FishMaterialIntake" || layer.Shader is not {} shader)
            return false;

        if (!shader.Mutable)
        {
            shader = shader.Duplicate();
            sprite.Comp.LayerSetShader(index, shader, "FishMaterialIntake");
        }

        var tint = insertion.MaterialColor ?? Color.White;
        shader.SetParameter("surface", tint.WithAlpha(insertion.FishIntakeOpacity));
        var remaining = insertion.EndTime - _fishTiming.CurTime;
        _sprite.LayerSetVisible(sprite, index, remaining > TimeSpan.Zero);
        var elapsed = Math.Clamp((duration - remaining).TotalSeconds, 0, duration.TotalSeconds);
        _sprite.LayerSetAnimationTime(sprite, index, (float) elapsed);
        return true;
    }
}
