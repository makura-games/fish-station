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
            !_powerCell.TryGetBatteryFromSlot(args.User, out _))
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

    private void StartDrink(EntityUid target, EntityUid user, FishBatteryDrinkerComponent drinker)
    {
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
            CancelDuplicate = false,
        });
    }

    private void OnDoAfter(Entity<FishBatteryDrinkerComponent> ent, ref FishBatteryDrinkerDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } source)
            return;

        if (!TryComp<BatteryComponent>(source, out _) ||
            !_powerCell.TryGetBatteryFromSlot(ent.Owner, out var drinkerBattery))
            return;

        TryComp<FishBatteryDrinkerSourceComponent>(source, out var sourceComp);

        var amount = ent.Comp.DrinkMultiplier * 1000f;
        var (sourceCharge, _) = _battery.GetCharge(source);
        var (drinkerCharge, drinkerMax) = _battery.GetCharge(drinkerBattery.Value.Owner);

        amount = MathF.Min(amount, sourceCharge);
        amount = MathF.Min(amount, drinkerMax - drinkerCharge);

        if (sourceComp is { MaxAmount: > 0 })
            amount = MathF.Min(amount, sourceComp.MaxAmount);

        if (amount <= 0)
        {
            _popup.PopupEntity(Loc.GetString("robot-drain-charge-empty", ("battery", source)), ent.Owner, ent.Owner);
            return;
        }

        if (drinkerCharge >= drinkerMax - 0.01f)
        {
            _popup.PopupEntity(Loc.GetString("robot-drain-charge-full"), ent.Owner, ent.Owner);
            return;
        }

        if (_battery.TryUseCharge(source, amount))
        {
            _battery.SetCharge(drinkerBattery.Value.AsNullable(), drinkerCharge + amount);
            _popup.PopupEntity(Loc.GetString("robot-drain-charge-success", ("battery", source)), ent.Owner, ent.Owner, PopupType.SmallCaution);

            if (sourceComp?.DrinkSound != null)
                _audio.PlayPvs(sourceComp.DrinkSound, source);

            // Повторная зарядка, пока не полный / источник не пуст (как цикл Paradise).
            StartDrink(source, ent.Owner, ent.Comp);
        }
    }
}
