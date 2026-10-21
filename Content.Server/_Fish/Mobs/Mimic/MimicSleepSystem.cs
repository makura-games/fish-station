using Content.Shared._Fish.Mobs.Mimic;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Mind;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Fish.Mobs.Mimic;

/// <summary>
/// Сон мимика работает на механике сна мимик спит, как персонаж в кровати
/// просыпается когда по нему кто-то кликнет и еще сон непробудный типо
/// (урон его не будит)и кнопки Проснуться у мимика тоже нету
/// </summary>
public sealed partial class MimicSleepSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        // ComponentStartup срабатывает после того как SleepingSystem усыпит.
        SubscribeLocalEvent<SleepingComponent, ComponentStartup>(OnSleepingStartup);
        SubscribeLocalEvent<SleepingComponent, SleepStateChangedEvent>(OnSleepStateChanged);
    }

    /// <summary>
    /// Пока мимик спит он не может двигаться
    /// </summary>
    private void OnSleepingStartup(Entity<SleepingComponent> ent, ref ComponentStartup args)
    {
        if (!HasComp<MimicJumpComponent>(ent))
            return;

        // Мимика можно будить только кликом
        if (ent.Comp.WakeAction is { } action && TryComp<ActionComponent>(action, out var actionComp))
        {
            _actions.RemoveAction((action, actionComp));
            ent.Comp.WakeAction = null;
            Dirty(ent, ent.Comp);
        }

        SetCanMove(ent, false);
    }

    /// <summary>
    /// Мимик проснулся
    /// </summary>
    private void OnSleepStateChanged(Entity<SleepingComponent> ent, ref SleepStateChangedEvent args)
    {
        if (args.FellAsleep)
            return;

        if (!HasComp<MimicJumpComponent>(ent))
            return;

        // Мимик оживает
        _mind.MakeSentient(ent.Owner, allowMovement: true, allowSpeech: false);
        SetCanMove(ent, true);

        // Действие прыжка появляется вместе с пробуждением.
        if (TryComp<MimicJumpComponent>(ent, out var jump))
            _actions.AddAction(ent.Owner, jump.ActionId);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Sunrise/Weapons/Melee/mimicry_swing.ogg"), ent);
        _popup.PopupEntity(Loc.GetString("mimic-wake-up"), ent.Owner, ent.Owner, PopupType.MediumCaution);
    }

    private void SetCanMove(Entity<SleepingComponent> ent, bool canMove)
    {
        if (TryComp<InputMoverComponent>(ent, out var mover))
            mover.CanMove = canMove;
    }
}
