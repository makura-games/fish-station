using Content.Client.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Species;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Client.Atmos.Overlays;

/// <summary>
///     Overlay responsible for rendering atmos fire animation.
/// </summary>
public sealed partial class GasTileFireOverlay : Overlay
{
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IEntityManager _entManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities | OverlaySpace.WorldSpaceBelowWorld;
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    private readonly SharedTransformSystem _xformSys;
    private readonly SharedMapSystem _mapSystem = default!;
    private readonly ShaderInstance _shader;

    private readonly float[][] _frameDelays;

    // TODO combine textures into a single texture atlas.
    private readonly Texture[][] _frames;

    private const int FireStates = 3;
    private const string FireRsiPath = "/Textures/Effects/fire.rsi";

    public const int GasOverlayZIndex = (int)Shared.DrawDepth.DrawDepth.Effects; // Under ghosts, above mostly everything else

    public GasTileFireOverlay()
    {
        IoCManager.InjectDependencies(this);
        _xformSys = _entManager.System<SharedTransformSystem>();
        _mapSystem = _entManager.System<SharedMapSystem>();
        _shader = _protoMan.Index(UnshadedShader).Instance();
        ZIndex = GasOverlayZIndex;

        _frameDelays = new float[FireStates][];
        _frames = new Texture[FireStates][];

        var fire = _resourceCache.GetResource<RSIResource>(FireRsiPath).RSI;

        for (var i = 0; i < FireStates; i++)
        {
            if (!fire.TryGetState((i + 1).ToString(), out var state))
                throw new ArgumentOutOfRangeException($"Fire RSI doesn't have state \"{i}\"!");

            _frames[i] = state.GetFrames(RsiDirection.South);
            _frameDelays[i] = state.GetDelays();
            InitializeFirePhases(i); // Fish-edit: смещаем первый кадр для соседних тайлов
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        var drawHandle = args.WorldHandle;
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();
        var overlayQuery = _entManager.GetEntityQuery<GasTileOverlayComponent>();
        var gridState = (args.WorldBounds,
            args.WorldHandle,
            _frames,
            _frameCounter,
            _shader,
            overlayQuery,
            xformQuery,
            _xformSys);

        var mapUid = _mapSystem.GetMapOrInvalid(args.MapId);

        if (args.Space != OverlaySpace.WorldSpaceEntities)
            return;

        // TODO: WorldBounds callback.
        _mapSystem.FindGridsIntersecting(args.MapId, args.WorldAABB, ref gridState,
            static (EntityUid uid, MapGridComponent grid,
                ref (Box2Rotated WorldBounds,
                    DrawingHandleWorld drawHandle,
                    Texture[][] frames,
                    int[] frameCounter,
                    ShaderInstance shader,
                    EntityQuery<GasTileOverlayComponent> overlayQuery,
                    EntityQuery<TransformComponent> xformQuery,
                    SharedTransformSystem xformSys) state) =>
            {
                if (!state.overlayQuery.TryGetComponent(uid, out var comp) ||
                    !state.xformQuery.TryGetComponent(uid, out var gridXform))
                {
                    return true;
                }

                var (_, _, worldMatrix, invMatrix) = state.xformSys.GetWorldPositionRotationMatrixWithInv(gridXform);
                state.drawHandle.SetTransform(worldMatrix);
                var floatBounds = invMatrix.TransformBox(state.WorldBounds).Enlarged(grid.TileSize);
                var localBounds = new Box2i(
                    (int)MathF.Floor(floatBounds.Left),
                    (int)MathF.Floor(floatBounds.Bottom),
                    (int)MathF.Ceiling(floatBounds.Right),
                    (int)MathF.Ceiling(floatBounds.Top));

                // Currently it would be faster to group drawing by gas rather than by chunk, but if the textures are
                // ever moved to a single atlas, that should no longer be the case. So this is just grouping draw calls
                // by chunk, even though its currently slower.

                state.drawHandle.UseShader(state.shader);
                foreach (var chunk in comp.Chunks.Values)
                {
                    var enumerator = new GasChunkEnumerator(chunk);

                    while (enumerator.MoveNext(out var gas))
                    {
                        if (gas.FireState == 0)
                            continue;

                        var index = chunk.Origin + (enumerator.X, enumerator.Y);
                        if (!localBounds.Contains(index))
                            continue;

                        var fireState = gas.FireState - 1;
                        var texture = state.frames[fireState][state.frameCounter[FirePhaseSlot(fireState, index)]]; // Fish-edit: фаза привязана к тайлу
                        state.drawHandle.DrawTexture(texture, index);
                    }
                }

                return true;
            });

        drawHandle.UseShader(null);
        drawHandle.SetTransform(Matrix3x2.Identity);
    }
}
