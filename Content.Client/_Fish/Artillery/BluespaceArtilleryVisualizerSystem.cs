using Content.Client.Camera;
using Content.Shared._Fish.Artillery;
using Robust.Client.Player;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client._Fish.Artillery;

public sealed partial class BluespaceArtilleryVisualizerSystem : VisualizerSystem<BluespaceArtilleryComponent>
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private CameraRecoilSystem _cameraRecoil = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private PointLightSystem _pointLight = default!;

    private const float ShakeRadius = 35.0f;
    private const float RecoilStrength = 20.0f;
    private const float FlashDuration = 0.3f;

    protected override void OnAppearanceChange(EntityUid uid, BluespaceArtilleryComponent component, ref AppearanceChangeEvent args)
    {
        if (_appearance.TryGetData(uid, BluespaceArtilleryVisuals.VisualState, out BluespaceArtilleryVisualState state))
        {
            if (TryComp<SpriteComponent>(uid, out var sprite))
            {
                switch (state)
                {
                    case BluespaceArtilleryVisualState.Idle:
                        sprite.LayerSetState(0, "idle");
                        break;
                    case BluespaceArtilleryVisualState.Charging:
                        sprite.LayerSetState(0, "charging");
                        break;
                    case BluespaceArtilleryVisualState.Fire:
                        sprite.LayerSetState(0, "fire");
                        TriggerRecoilIfNearby(uid);
                        TriggerMuzzleFlash(uid);
                        break;
                }
            }
        }
    }

    private void TriggerRecoilIfNearby(EntityUid artillery)
    {
        var player = _playerManager.LocalPlayer?.ControlledEntity;
        if (player == null)
            return;

        var artilleryXform = Transform(artillery).MapPosition;
        var playerXform = Transform(player.Value).MapPosition;

        if (artilleryXform.MapId != playerXform.MapId)
            return;

        float dist = (artilleryXform.Position - playerXform.Position).Length();
        if (dist <= ShakeRadius)
        {
            _cameraRecoil.KickCamera(player.Value, new Vector2(RecoilStrength, 0f));
        }
    }

    private void TriggerMuzzleFlash(EntityUid artillery)
    {
        if (_pointLight == null)
        {
            Log.Error("PointLightSystem is null!");
            return;
        }

        if (TryComp<PointLightComponent>(artillery, out var light))
        {
            _pointLight.SetEnabled(artillery, true, light);
            Timer.Spawn(TimeSpan.FromSeconds(FlashDuration), () =>
            {
                if (!Deleted(artillery) && TryComp<PointLightComponent>(artillery, out var lightAfter))
                    _pointLight.SetEnabled(artillery, false, lightAfter);
            });
        }
    }
}