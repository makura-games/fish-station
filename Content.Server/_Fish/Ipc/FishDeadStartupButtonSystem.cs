using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Shared._Fish.Ipc;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
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
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private EuiManager _eui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishDeadStartupButtonComponent, GetVerbsEvent<AlternativeVerb>>(AddVerb);
        SubscribeLocalEvent<FishDeadStartupButtonComponent, FishDeadStartupDoAfterEvent>(OnDoAfter);
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
        {
            _mobState.ChangeMobState(uid, MobState.Alive, mobState);
            _popup.PopupEntity(Loc.GetString("fish-ipc-startup-success", ("target", uid)), uid);
            _audio.PlayPvs(component.Sound, uid);

            // Как у дефиба: предложить призраку вернуться в тело.
            // Успех только здесь: любой переход в Alive (jumpstarter, хил) — не перезапуск.
            if (_mind.TryGetMind(uid, out _, out var mindComp) &&
                mindComp.UserId is { } userId &&
                _player.TryGetSessionById(userId, out var playerSession) &&
                mindComp.CurrentEntity != uid)
            {
                _eui.OpenEui(new ReturnToBodyEui(mindComp, _mind, _player), playerSession);
            }
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("fish-ipc-startup-failed", ("target", uid)), uid);
            Spawn("EffectSparks", Transform(uid).Coordinates);
        }

        args.Handled = true;
    }
}
