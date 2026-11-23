using Content.Client.Graphics;
using Content.Client.Overlays;
using Content.Client.Viewport;
using Content.Shared._Fish.Ipc;
using Content.Shared._Fish.Netrunner;
using Content.Shared.Inventory.Events;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Fish.Netrunner;

/// <summary>Outlines mindshield wearers, cyborgs, and IPCs with a light-sensitive blue-cyan contour while wearing the netrunner visor.</summary>
public sealed partial class ShowNetrunnerMindShieldIconsSystem : EquipmentHudSystem<ShowNetrunnerMindShieldIconsComponent>
{
    private static readonly ProtoId<ShaderPrototype> OutlineShader = "FishNetrunnerMindShieldOutline";
    private const string PostShaderId = "FishNetrunnerMindShieldOutline";
    private const float DefaultWidth = 1f;

    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery = default!;
    [Dependency] private EntityQuery<SpriteComponent> _spriteQuery = default!;

    private readonly Dictionary<int, ShaderInstance> _shaderCache = new();
    private readonly HashSet<EntityUid> _outlined = new();
    private readonly HashSet<EntityUid> _current = new();
    private readonly List<EntityUid> _toRemove = new();

    public override void Shutdown()
    {
        ClearOutlines();
        foreach (var shader in _shaderCache.Values)
            shader.Dispose();
        _shaderCache.Clear();
        base.Shutdown();
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<ShowNetrunnerMindShieldIconsComponent> args)
    {
        base.UpdateInternal(args);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();
        ClearOutlines();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!IsActive)
        {
            if (_outlined.Count > 0)
                ClearOutlines();
            return;
        }

        var renderScale = Math.Max(1, _eye.MainViewport?.GetRenderScale() ?? 1);
        var shader = GetShader(renderScale);

        _current.Clear();
        var mindshieldQuery = EntityQueryEnumerator<MindShieldStatusComponent, SpriteComponent>();
        while (mindshieldQuery.MoveNext(out var uid, out var status, out var sprite))
        {
            if (status.IsMindshielded && _mobStateQuery.HasComp(uid))
                OutlineTarget(uid, sprite, shader);
        }

        var borgQuery = EntityQueryEnumerator<BorgChassisComponent, SpriteComponent>();
        while (borgQuery.MoveNext(out var uid, out _, out var sprite))
        {
            if (_mobStateQuery.HasComp(uid))
                OutlineTarget(uid, sprite, shader);
        }

        var ipcQuery = EntityQueryEnumerator<FishIpcComponent, SpriteComponent>();
        while (ipcQuery.MoveNext(out var uid, out _, out var sprite))
        {
            if (_mobStateQuery.HasComp(uid))
                OutlineTarget(uid, sprite, shader);
        }

        if (_outlined.Count > _current.Count)
        {
            _toRemove.Clear();
            foreach (var uid in _outlined)
            {
                if (!_current.Contains(uid))
                    _toRemove.Add(uid);
            }

            foreach (var uid in _toRemove)
            {
                if (!Deleted(uid) && _spriteQuery.TryComp(uid, out var sprite))
                    _sprite.RemovePostShader((uid, sprite), PostShaderId);
                _outlined.Remove(uid);
            }
        }
    }

    private void OutlineTarget(EntityUid uid, SpriteComponent sprite, ShaderInstance shader)
    {
        _current.Add(uid);
        _outlined.Add(uid);

        if (_sprite.TryGetPostShader((uid, sprite), PostShaderId, out var existing) && existing.Shader == shader)
            return;

        _sprite.SetPostShader((uid, sprite), new SpriteComponent.PostShaderArgs(PostShaderId, shader)
        {
            After = ContentPostShaderIds.AfterBaseEffects,
        });
    }

    private void ClearOutlines()
    {
        foreach (var uid in _outlined)
        {
            if (!Deleted(uid) && _spriteQuery.TryComp(uid, out var sprite))
                _sprite.RemovePostShader((uid, sprite), PostShaderId);
        }

        _outlined.Clear();
        _current.Clear();
    }

    private ShaderInstance GetShader(int renderScale)
    {
        if (_shaderCache.TryGetValue(renderScale, out var shader))
            return shader;

        shader = _proto.Index(OutlineShader).InstanceUnique();
        shader.SetParameter("outline_width", DefaultWidth * renderScale);
        _shaderCache.Add(renderScale, shader);
        return shader;
    }
}
