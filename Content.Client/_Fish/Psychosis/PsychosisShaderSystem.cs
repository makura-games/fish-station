using Content.Shared._Fish.Psychosis;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameStates;
using Robust.Shared.IoC;
using Robust.Shared.Player;

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
        SubscribeLocalEvent<PsychosisComponent, ComponentStartup>(OnPsychosisStartup);
        SubscribeLocalEvent<PsychosisComponent, AfterAutoHandleStateEvent>(OnPsychosisState);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalPlayerDetached);
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
        var uid = GetEntity(ev.Uid);
        if (uid != _player.LocalSession?.AttachedEntity)
            return;

        ApplyLevel(uid, ev.Level, ev.IsInitialState);
    }

    private void OnPsychosisStartup(Entity<PsychosisComponent> ent, ref ComponentStartup args)
    {
        if (_player.LocalSession?.AttachedEntity == ent.Owner)
            ApplyLevel(ent.Owner, ent.Comp.Current, true);
    }

    private void OnPsychosisState(Entity<PsychosisComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalSession?.AttachedEntity == ent.Owner)
            ApplyLevel(ent.Owner, ent.Comp.Current, true);
    }

    private void OnLocalPlayerAttached(LocalPlayerAttachedEvent ev)
    {
        var level = TryComp<PsychosisComponent>(ev.Entity, out var psychosis) ? psychosis.Current : 0f;
        ApplyLevel(ev.Entity, level, true);
    }

    private void OnLocalPlayerDetached(LocalPlayerDetachedEvent ev)
    {
        ApplyLevel(ev.Entity, 0f, true);
    }

    private void ApplyLevel(EntityUid uid, float level, bool isInitialState)
    {
        _overlay.SetIntensity(level / 100f);
        RaiseLocalEvent(new PsychosisUpdateEvent(GetNetEntity(uid), level, isInitialState));
    }
}