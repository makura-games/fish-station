using Content.Server.Popups;
using Content.Shared._Fish.Ipc;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Server._Fish.Ipc;

/// <summary>
/// Зарядка КПБ от ЛКП через альтернативный verb (Paradise APC power cord).
/// </summary>
public sealed partial class FishBatteryDrinkerSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BatteryComponent, GetVerbsEvent<AlternativeVerb>>(AddAltVerb);
        SubscribeLocalEvent<FishBatteryDrinkerComponent, FishBatteryDrinkerDoAfterEvent>(OnDoAfter);
    }

    private void AddAltVerb(EntityUid uid, BatteryComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!TryComp<FishBatteryDrinkerComponent>(args.User, out var drinker) ||
            !TestDrinkable(uid, drinker) ||
            !_powerCell.TryGetBatteryFromSlot(args.User, out var drinkerBattery))
            return;

        // На полной батарее verb не даём — иначе спам звуком/попапами.
        if (IsSelfFull(drinkerBattery.Value.Owner, drinker))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => StartDrink(uid, args.User, drinker),
            Text = Loc.GetString("robot-drain-name"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/smite.svg.192dpi.png")),
        });
    }

    private bool TestDrinkable(EntityUid target, FishBatteryDrinkerComponent drinker)
    {
        return drinker.DrinkAll || HasComp<FishBatteryDrinkerSourceComponent>(target);
    }

    private bool IsSelfFull(EntityUid battery, FishBatteryDrinkerComponent drinker)
    {
        var (charge, max) = _battery.GetCharge(battery);
        if (max <= 0f)
            return true;

        return charge / max >= drinker.MaxSelfChargeFraction - 0.001f;
    }

    private void StartDrink(EntityUid target, EntityUid user, FishBatteryDrinkerComponent drinker)
    {
        if (!_powerCell.TryGetBatteryFromSlot(user, out var drinkerBattery) ||
            IsSelfFull(drinkerBattery.Value.Owner, drinker))
        {
            _popup.PopupEntity(Loc.GetString("robot-drain-charge-full"), user, user);
            return;
        }

        var time = drinker.DrinkSpeed;
        if (TryComp<FishBatteryDrinkerSourceComponent>(target, out var source))
            time *= source.DrinkSpeedMulti;
        else
            time *= 2.5f;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, time, new FishBatteryDrinkerDoAfterEvent(), user, target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            DistanceThreshold = 1.35f,
            NeedHand = true,
            // Без этого verb спамит параллельные циклы и sparks/звук.
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        });
    }

    private void OnDoAfter(Entity<FishBatteryDrinkerComponent> ent, ref FishBatteryDrinkerDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } source)
            return;

        if (!TestDrinkable(source, ent.Comp) ||
            !TryComp<BatteryComponent>(source, out _) ||
            !_powerCell.TryGetBatteryFromSlot(ent.Owner, out var drinkerBattery))
            return;

        if (source == drinkerBattery.Value.Owner)
            return;

        TryComp<FishBatteryDrinkerSourceComponent>(source, out var sourceComp);

        var (sourceCharge, _) = _battery.GetCharge(source);
        var (drinkerCharge, drinkerMax) = _battery.GetCharge(drinkerBattery.Value.Owner);

        if (drinkerMax <= 0f || drinkerCharge / drinkerMax >= ent.Comp.MaxSelfChargeFraction - 0.001f)
        {
            _popup.PopupEntity(Loc.GetString("robot-drain-charge-full"), ent.Owner, ent.Owner);
            return;
        }

        // 5% от ёмкости шасси за один цикл (адаптер ускоряет do-after, не размер порции).
        var amount = drinkerMax * ent.Comp.DrinkPercent;
        // DrinkMultiplier — legacy YAML; норма = 1.
        if (ent.Comp.DrinkMultiplier is > 0f and not 1f)
            amount *= ent.Comp.DrinkMultiplier;

        var targetCharge = drinkerMax * ent.Comp.MaxSelfChargeFraction;
        amount = MathF.Min(amount, sourceCharge);
        amount = MathF.Min(amount, targetCharge - drinkerCharge);

        if (sourceComp is { MaxAmount: > 0 })
            amount = MathF.Min(amount, sourceComp.MaxAmount);

        if (amount <= 0)
        {
            _popup.PopupEntity(Loc.GetString("robot-drain-charge-full"), ent.Owner, ent.Owner);
            return;
        }

        if (!_battery.TryUseCharge(source, amount))
            return;

        _battery.SetCharge(drinkerBattery.Value.AsNullable(), drinkerCharge + amount);
        _popup.PopupEntity(Loc.GetString("robot-drain-charge-success", ("battery", source)), ent.Owner, ent.Owner, PopupType.Small);

        Spawn("EffectSparks", Transform(source).Coordinates);
        if (sourceComp?.DrinkSound != null)
            _audio.PlayPvs(sourceComp.DrinkSound, source);

        // Полный — стоп цикла, без повторного do-after (иначе спам sparks).
        if (IsSelfFull(drinkerBattery.Value.Owner, ent.Comp))
        {
            _popup.PopupEntity(Loc.GetString("robot-drain-charge-full"), ent.Owner, ent.Owner);
            return;
        }

        StartDrink(source, ent.Owner, ent.Comp);
    }
}
