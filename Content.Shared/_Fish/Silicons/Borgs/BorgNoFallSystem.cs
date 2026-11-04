using Content.Shared._Fish.Silicons.Borgs.Components;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Silicons.Borgs;

/// <summary>
/// Fish: предотвращает падение борга Mk2 в критическом/мёртвом состоянии,
/// гасит его фонарь и запускает искры при входе в крит.
/// Ванильные борги (без <see cref="BorgNoFallComponent"/>) не затрагиваются.
/// </summary>
public sealed partial class BorgNoFallSystem : EntitySystem
{
    [Dependency] private SharedHandheldLightSystem _handheldLight = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;

    private static readonly EntProtoId CritSparksEffect = "EffectMechSparks";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgNoFallComponent, DownAttemptEvent>(OnDownAttempt);
        SubscribeLocalEvent<BorgNoFallComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    /// <summary>
    /// Отменяет падение борга Mk2 при входе в крит/смерть.
    /// DownAttemptEvent — класс (CancellableEntityEventArgs), не struct.
    /// Поднимается через RaiseLocalEvent(uid, msg, false) без ref, поэтому
    /// сигнатура (EntityUid, Comp, Event) — Entity<> оверлоад требует ref и не подходит.
    /// Живой борг Mk2 должен опрокидываться штатно (оглушение, гравитация и т.д.).
    /// </summary>
    private void OnDownAttempt(EntityUid uid, BorgNoFallComponent component, DownAttemptEvent args)
    {
        if (!_mobState.IsIncapacitated(uid))
            return;

        args.Cancel();
    }

    /// <summary>
    /// Гасит фонарь и запускает искры при переходе борга Mk2 в крит/смерть.
    /// Фонарь в критическом состоянии не горит и не накладывает текстуру на
    /// wreck-спрайт; после реанимации игрок включит сам.
    /// Искры — разовые, MobStateChangedEvent срабатывает один раз на переход;
    /// спавним только на сервере: EffectMechSparks (TimedDespawn 0.5s)
    /// реплицируется клиентам сам, локальная копия не нужна.
    /// </summary>
    private void OnMobStateChanged(Entity<BorgNoFallComponent> uid, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        if (TryComp<HandheldLightComponent>(uid, out var light))
            _handheldLight.TurnOff((uid.Owner, light), makeNoise: false);

        if (args.NewMobState == MobState.Critical && _net.IsServer)
            Spawn(CritSparksEffect, Transform(uid).Coordinates);
    }
}
