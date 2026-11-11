using System.Numerics;
using Content.Shared._Fish.Stress;
using Content.Shared.Popups;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.IoC;
using Robust.Shared.Localization;

namespace Content.Client._Fish.Stress;

public sealed class StressClientSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private StressOverlay _overlay = default!;
    private StressHudOverlay _hudOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new StressOverlay();
        IoCManager.InjectDependencies(_overlay);
        _overlayManager.AddOverlay(_overlay);
        _hudOverlay = new StressHudOverlay();
        _overlayManager.AddOverlay(_hudOverlay);

        SubscribeNetworkEvent<StressEffectsEvent>(OnStressEffects);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay(_overlay);
        _overlayManager.RemoveOverlay(_hudOverlay);
    }

    private void OnStressEffects(StressEffectsEvent ev)
    {
        if (GetEntity(ev.Uid) != _player.LocalSession?.AttachedEntity)
            return;

        // Если событие активно, включаем эффект (интенсивность выставится в FrameUpdate)
        _overlay.SetIntensity(ev.Active ? 1f : 0f);
        if (ev.Active && _player.LocalSession?.AttachedEntity is { } entity)
            _popup.PopupClient(Loc.GetString("fish-stress-critical"), entity, entity);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // Плавное управление интенсивностью в зависимости от текущего стресса
        var entity = _player.LocalSession?.AttachedEntity;
        if (entity == null || !TryComp<StressComponent>(entity.Value, out var stress))
        {
            _overlay.SetIntensity(0f);
            _hudOverlay.SetStress(null);
            return;
        }

        float target = stress.Current > 75f ? (stress.Current - 75f) / 25f : 0f;
        _overlay.SetIntensity(target);
        _hudOverlay.SetStress(stress.Current);
    }
}