using Content.Shared._Fish.ClockworkCult.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.ClockworkCult.Systems;

/// <summary>
/// Минимальные clockwork-ловушки: skewer, steam vent, pressure sensor, lever, repeater.
/// </summary>
public sealed partial class FishClockworkTrapSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private FishClockworkServantSystem _servant = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<DamageTypePrototype> PiercingDamage = "Piercing";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishClockworkTrapComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<FishClockworkTrapComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnStartCollide(Entity<FishClockworkTrapComponent> ent, ref StartCollideEvent args)
    {
        var other = args.OtherEntity;
        if (!HasComp<MobStateComponent>(other))
            return;

        switch (ent.Comp.Kind)
        {
            case FishClockworkTrapKind.PressureSensor:
                TryTriggerSensor(ent, other);
                break;
            case FishClockworkTrapKind.BrassSkewer:
                TrySkewer(ent, other);
                break;
            case FishClockworkTrapKind.SteamVent:
                TrySteamVent(ent, other);
                break;
        }
    }

    private void OnActivate(Entity<FishClockworkTrapComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (!_servant.IsServant(args.User))
            return;

        switch (ent.Comp.Kind)
        {
            case FishClockworkTrapKind.Lever:
            case FishClockworkTrapKind.Repeater:
                TriggerLinked(ent);
                args.Handled = true;
                _popup.PopupEntity(Loc.GetString("fish-clockwork-trap-activated"), args.User, args.User);
                break;
            case FishClockworkTrapKind.PressureSensor:
                TriggerLinked(ent);
                args.Handled = true;
                break;
        }
    }

    private bool CanTrigger(Entity<FishClockworkTrapComponent> ent)
    {
        if (ent.Comp.NextTriggerTime != null && ent.Comp.NextTriggerTime > _timing.CurTime)
            return false;

        return true;
    }

    private void MarkTriggered(Entity<FishClockworkTrapComponent> ent)
    {
        ent.Comp.NextTriggerTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.CooldownSeconds);
    }

    private void TryTriggerSensor(Entity<FishClockworkTrapComponent> ent, EntityUid victim)
    {
        if (_servant.IsServant(victim))
            return;

        if (!CanTrigger(ent))
            return;

        MarkTriggered(ent);
        TriggerLinked(ent);
        _popup.PopupEntity(Loc.GetString("fish-clockwork-trap-sensor"), victim, PopupType.SmallCaution);
    }

    private void TriggerLinked(Entity<FishClockworkTrapComponent> source)
    {
        var coords = Transform(source).Coordinates;
        foreach (var trap in _lookup.GetEntitiesInRange<FishClockworkTrapComponent>(coords, source.Comp.TriggerRange))
        {
            if (trap.Owner == source.Owner)
                continue;

            switch (trap.Comp.Kind)
            {
                case FishClockworkTrapKind.BrassSkewer:
                    // Активируем skewer «в воздух» — бьёт ближайшего неслугу в радиусе.
                    foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(trap).Coordinates, 0.75f))
                    {
                        if (!_servant.IsServant(mob.Owner))
                            TrySkewer(trap, mob.Owner);
                    }
                    break;
                case FishClockworkTrapKind.SteamVent:
                    foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(trap).Coordinates, 1.5f))
                    {
                        if (!_servant.IsServant(mob.Owner))
                            TrySteamVent(trap, mob.Owner);
                    }
                    break;
            }
        }
    }

    private void TrySkewer(Entity<FishClockworkTrapComponent> ent, EntityUid victim)
    {
        if (_servant.IsServant(victim))
            return;

        if (!CanTrigger(ent))
            return;

        MarkTriggered(ent);

        var dmg = new DamageSpecifier();
        dmg.DamageDict[PiercingDamage] = ent.Comp.SkewerDamage;
        _damageable.TryChangeDamage(victim, dmg, origin: ent.Owner);
        _stun.TryUpdateParalyzeDuration(victim, TimeSpan.FromSeconds(ent.Comp.SkewerStunSeconds));
        _popup.PopupEntity(Loc.GetString("fish-clockwork-trap-skewer"), victim, PopupType.MediumCaution);
    }

    private void TrySteamVent(Entity<FishClockworkTrapComponent> ent, EntityUid victim)
    {
        if (_servant.IsServant(victim))
            return;

        if (!CanTrigger(ent))
            return;

        MarkTriggered(ent);

        // Минимальный эффект: дым + popup (блокировка видимости упрощена).
        // Fish-edit: длительность из компонента, а не литерал.
        Spawn("Smoke", Transform(ent).Coordinates);
        _popup.PopupEntity(Loc.GetString("fish-clockwork-trap-steam"), victim, PopupType.MediumCaution);
        _stun.TryKnockdown(victim, TimeSpan.FromSeconds(ent.Comp.SteamDurationSeconds), refresh: true);
    }
}

