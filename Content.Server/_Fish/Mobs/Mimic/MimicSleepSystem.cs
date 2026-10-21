using Content.Shared._Fish.Mobs.Mimic;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Mobs.Mimic;

public sealed partial class MimicSleepSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SleepingComponent, ComponentStartup>(OnSleepingStartup);
        SubscribeLocalEvent<SleepingComponent, SleepStateChangedEvent>(OnSleepStateChanged);
        SubscribeLocalEvent<SleepingComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
    }

    private void OnSleepingStartup(Entity<SleepingComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<MimicJumpComponent>(ent, out var jump))
            return;

        if (ent.Comp.WakeAction is { } action && TryComp<ActionComponent>(action, out var actionComp))
        {
            _actions.RemoveAction((action, actionComp));
            ent.Comp.WakeAction = null;
            Dirty(ent, ent.Comp);
        }

        jump.Awake = false;
        _actionBlocker.UpdateCanMove(ent.Owner);
    }

    private void OnUpdateCanMove(Entity<SleepingComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (!TryComp<MimicJumpComponent>(ent, out var jump) || jump.Awake)
            return;

        args.Cancel();
    }

    private void OnSleepStateChanged(Entity<SleepingComponent> ent, ref SleepStateChangedEvent args)
    {
        if (args.FellAsleep)
            return;

        if (!TryComp<MimicJumpComponent>(ent, out var jump))
            return;

        if (TerminatingOrDeleted(ent.Owner))
            return;

        if (_mobState.IsDead(ent))
            return;

        jump.Awake = true;

        _mind.MakeSentient(ent.Owner, allowMovement: true, allowSpeech: false);

        _actions.AddAction(ent.Owner, ref jump.JumpAction, jump.ActionId);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Sunrise/Weapons/Melee/mimicry_swing.ogg"), ent);

        Timer.Spawn(0, () =>
        {
            if (Exists(ent.Owner))
                _actionBlocker.UpdateCanMove(ent.Owner);
        });
    }
}
