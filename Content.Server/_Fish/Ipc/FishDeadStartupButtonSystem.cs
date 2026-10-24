using Content.Shared._Fish.Ipc;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Server._Fish.Ipc;

/// <summary>Перезапуск отключённого шасси (EE DeadStartupButton + Paradise revive-by-healing).</summary>
public sealed partial class FishDeadStartupButtonSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishDeadStartupButtonComponent, GetVerbsEvent<AlternativeVerb>>(AddVerb);
        SubscribeLocalEvent<FishDeadStartupButtonComponent, FishDeadStartupDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<FishDeadStartupButtonComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void AddVerb(EntityUid uid, FishDeadStartupButtonComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!_mobState.IsDead(uid) || !args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Act = () =>
            {
                _audio.PlayPvs(component.Sound, uid);
                _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, component.DoAfterDelay,
                    new FishDeadStartupDoAfterEvent(), uid, uid)
                {
                    BreakOnDamage = true,
                    BreakOnMove = true,
                });
            },
            Text = Loc.GetString("fish-ipc-startup-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/Spare/poweronoff.svg.192dpi.png")),
            Priority = 2,
        });
    }

    private void OnDoAfter(EntityUid uid, FishDeadStartupButtonComponent component, FishDeadStartupDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled
            || !TryComp<MobStateComponent>(uid, out var mobState)
            || !_mobState.IsDead(uid, mobState)
            || !TryComp<MobThresholdsComponent>(uid, out var thresholds)
            || !_mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var criticalThreshold, thresholds))
            return;

        if (_damageable.GetTotalDamage(uid) < criticalThreshold)
            _mobState.ChangeMobState(uid, MobState.Alive, mobState);
        else
        {
            _popup.PopupEntity(Loc.GetString("fish-ipc-startup-failed", ("target", uid)), uid);
            Spawn("EffectSparks", Transform(uid).Coordinates);
        }

        args.Handled = true;
    }

    private void OnMobStateChanged(EntityUid uid, FishDeadStartupButtonComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            return;

        _popup.PopupEntity(Loc.GetString("fish-ipc-startup-success", ("target", uid)), uid);
        _audio.PlayPvs(component.Sound, uid);
    }
}
