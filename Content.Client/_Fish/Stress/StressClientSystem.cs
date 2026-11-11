using System.Numerics;
using Content.Shared._Fish.Stress;
using Content.Shared.Popups;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Graphics;

namespace Content.Client._Fish.Stress;

public sealed partial class StressClientSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IEyeManager _eyeManager = default!;

    private StressOverlay _overlay = default!;
    private StressHudOverlay _hudOverlay = default!;
    private IEye? _shakeEye;
    private Vector2 _appliedCameraShake;
    private float _shakePhase;

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
        RestoreCameraShake();
        base.Shutdown();
        _overlayManager.RemoveOverlay(_overlay);
        _overlayManager.RemoveOverlay(_hudOverlay);
    }

    /// <summary>
    /// Обновляет визуальное состояние стресса и показывает предупреждение игроку.
    /// </summary>
    private void OnStressEffects(StressEffectsEvent ev)
    {
        if (GetEntity(ev.Uid) != _player.LocalSession?.AttachedEntity)
            return;

        // Если событие активно, включаем эффект (интенсивность выставится в FrameUpdate)
        _overlay.SetIntensity(ev.Active ? 1f : 0f);
        if (ev.Active && _player.LocalSession?.AttachedEntity is { } entity)
            _popup.PopupClient(Loc.GetString("fish-stress-critical"), entity, entity);
    }

    /// <summary>
    /// Обновляет шкалу стресса и интенсивность визуальных и камерных эффектов.
    /// </summary>
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // Плавное управление интенсивностью в зависимости от текущего стресса
        var entity = _player.LocalSession?.AttachedEntity;
        if (entity == null || !TryComp<StressComponent>(entity.Value, out var stress))
        {
            _overlay.SetIntensity(0f);
            _hudOverlay.SetStress(null);
            UpdateCameraShake(frameTime, 0f);
            return;
        }

        float target = stress.Current > 75f ? (stress.Current - 75f) / 25f : 0f;
        _overlay.SetIntensity(target);
        _hudOverlay.SetStress(stress.Current);
        UpdateCameraShake(frameTime, target);
    }

    /// <summary>
    /// Применяет дрожь камеры, сохраняя посторонние смещения её позиции.
    /// </summary>
    private void UpdateCameraShake(float frameTime, float intensity)
    {
        var eye = _eyeManager.CurrentEye;
        if (_shakeEye != null && !ReferenceEquals(_shakeEye, eye))
            RestoreCameraShake();

        if (intensity <= 0f)
        {
            RestoreCameraShake();
            return;
        }

        _shakeEye ??= eye;
        _shakePhase += frameTime * 24f;
        var previousShake = _appliedCameraShake;
        var externalOffset = eye.Offset - previousShake;
        var amplitude = Math.Clamp(intensity, 0f, 1f) * 0.1f;
        var shake = new Vector2(
            MathF.Sin(_shakePhase),
            MathF.Cos(_shakePhase * 1.37f)) * amplitude;

        eye.Offset = externalOffset + shake;
        _appliedCameraShake = shake;
    }

    /// <summary>
    /// Убирает только смещение, добавленное эффектом стресса.
    /// </summary>
    private void RestoreCameraShake()
    {
        if (_shakeEye == null)
            return;

        _shakeEye.Offset -= _appliedCameraShake;
        _shakeEye = null;
        _appliedCameraShake = Vector2.Zero;
    }
}