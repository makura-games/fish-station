using Content.Shared._Fish.MentalHealth;
using Content.Shared._Fish.Neuro;
using Content.Shared.Popups;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.IoC;
using Robust.Shared.Localization;

namespace Content.Client._Fish.Neuro;

/// <summary>
/// Применяет визуальные эффекты (шейдеры) при низких нейронах.
/// </summary>
public sealed partial class NeuroEffectsSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private NeuroOverlay _overlay = new();

    public override void Initialize()
    {
        base.Initialize();
        IoCManager.InjectDependencies(_overlay);
        _overlayManager.AddOverlay(_overlay);
        SubscribeNetworkEvent<NeuroAlertEvent>(OnNeuroAlert);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay(_overlay);
    }

    private void OnNeuroAlert(NeuroAlertEvent ev)
    {
        var entity = _player.LocalSession?.AttachedEntity;
        if (entity == null)
            return;

        _popup.PopupClient(Loc.GetString(ev.Message), entity.Value, entity.Value);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var entity = _player.LocalSession?.AttachedEntity;
        if (entity == null || !TryComp<NeuroComponent>(entity.Value, out var neuro))
        {
            _overlay.SetIntensity(0f);
            return;
        }

        var intensity = neuro.Current switch
        {
            < MentalHealthConstants.NeuroCritical => 1f,
            < MentalHealthConstants.NeuroModerate =>
                (MentalHealthConstants.NeuroModerate - neuro.Current)
                / (MentalHealthConstants.NeuroModerate - MentalHealthConstants.NeuroCritical) * 0.7f,
            < MentalHealthConstants.NeuroMild =>
                (MentalHealthConstants.NeuroMild - neuro.Current)
                / (MentalHealthConstants.NeuroMild - MentalHealthConstants.NeuroModerate) * 0.3f,
            _ => 0f,
        };

        _overlay.SetIntensity(intensity);
    }
}
