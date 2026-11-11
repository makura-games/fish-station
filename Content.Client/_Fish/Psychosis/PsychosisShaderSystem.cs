using Content.Shared._Fish.Psychosis;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Enums;

namespace Content.Client._Fish.Psychosis;

/// <summary>
/// Применяет шейдеры психоза на основе уровня.
/// </summary>
public sealed partial class PsychosisShaderSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;

    private PsychosisOverlay _overlay = new();

    public override void Initialize()
    {
        base.Initialize();
        IoCManager.InjectDependencies(_overlay);
        _overlayManager.AddOverlay(_overlay);
        SubscribeNetworkEvent<PsychosisUpdateEvent>(OnPsychosisUpdate);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay(_overlay);
    }

    /// <summary>
    /// Передаёт уровень психоза локального игрока экранному overlay.
    /// </summary>
    private void OnPsychosisUpdate(PsychosisUpdateEvent ev)
    {
        if (GetEntity(ev.Uid) != _player.LocalSession?.AttachedEntity)
            return;
        _overlay.SetIntensity(ev.Level / 100f);
    }
}