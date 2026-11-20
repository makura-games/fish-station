using System.Numerics;
using Content.Client._Fish.Homelander; // Fish-edit
using Content.Shared.Examine;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client.Popups;

/// <summary>
/// Draws popup text, either in world or on screen.
/// </summary>
public sealed class PopupOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    private readonly IConfigurationManager _configManager;
    private readonly IEntityManager _entManager;
    private readonly IPlayerManager _playerMgr;
    private readonly IUserInterfaceManager _uiManager;
    private readonly PopupSystem _popup;
    private readonly PopupUIController _controller;
    private readonly ExamineSystemShared _examine;
    private readonly SharedTransformSystem _transform;
    private readonly ShaderInstance _shader;
    private readonly HomelanderSystem _homelander; // Fish-edit

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public PopupOverlay(
        IConfigurationManager configManager,
        IEntityManager entManager,
        IPlayerManager playerMgr,
        IPrototypeManager protoManager,
        IUserInterfaceManager uiManager,
        PopupUIController controller,
        ExamineSystemShared examine,
        SharedTransformSystem transform,
        PopupSystem popup)
    {
        _configManager = configManager;
        _entManager = entManager;
        _playerMgr = playerMgr;
        _uiManager = uiManager;
        _examine = examine;
        _transform = transform;
        _popup = popup;
        _controller = controller;
        _homelander = entManager.System<HomelanderSystem>(); // Fish-edit

        _shader = protoManager.Index(UnshadedShader).Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        args.DrawingHandle.SetTransform(Matrix3x2.Identity);
        args.DrawingHandle.UseShader(_shader);
        var scale = _configManager.GetCVar(CVars.DisplayUIScale);

        if (scale == 0f)
            scale = _uiManager.DefaultUIScale;

        DrawWorld(args.ScreenHandle, args, scale);

        args.DrawingHandle.UseShader(null);
    }

    private void DrawWorld(DrawingHandleScreen worldHandle, OverlayDrawArgs args, float scale)
    {
        if (_popup.WorldLabels.Count == 0 || args.ViewportControl == null)
            return;

        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var ourEntity = _playerMgr.LocalEntity;
        var viewPos = new MapCoordinates(args.WorldAABB.Center, args.MapId);
        var ourPos = args.WorldBounds.Center;
        if (ourEntity != null)
        {
            viewPos = _transform.GetMapCoordinates(ourEntity.Value);
            ourPos = viewPos.Position;
        }

        foreach (var (popup, entity) in _popup.EnumerateWorldLabelsWithAnchors()) // Fish-edit
        {
            var mapPos = _transform.ToMapCoordinates(popup.InitialPos);
            // Fish-start
            var anchor = entity ?? popup.InitialPos.EntityId;
            if (_homelander.TryGetFlightPopupOffset(anchor, args.Viewport.Eye?.Rotation ?? Angle.Zero, out var textOffset) &&
                entity.HasValue)
                mapPos = _transform.GetMapCoordinates(anchor);
            // Fish-end

            if (mapPos.MapId != args.MapId)
                continue;

            var distance = (mapPos.Position - ourPos).Length();

            // Should handle fade here too wyci.
            if (!args.WorldBounds.Contains(mapPos.Position) || !_examine.InRangeUnOccluded(viewPos, mapPos, distance,
                    e => e == popup.InitialPos.EntityId || e == ourEntity))
                continue;

            // Sunrise edit start
            var horizontalDirection = 0f;
            if (ourEntity.HasValue)
            {
                var moverCoords = _transform.GetMoverCoordinates(ourEntity.Value);
                // float horizontalDirection = 0f -1 for left, 1 for right.
                horizontalDirection = moverCoords.X <= popup.InitialPos.X ? 1f : -1f;
            }
            // Sunrise edit end

            var pos = Vector2.Transform(mapPos.Position + textOffset, matrix); // Fish-edit
            _controller.DrawPopup(popup, worldHandle, pos, scale, horizontalDirection); // Sunrise edit
        }
    }
}
