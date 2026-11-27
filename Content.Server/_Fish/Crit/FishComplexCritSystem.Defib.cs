using Content.Shared._Fish.Crit;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Popups;

namespace Content.Server._Fish.Crit;

public sealed partial class FishComplexCritSystem
{
    private void InitializeDefib()
    {
        SubscribeLocalEvent<FishComplexCritComponent, TargetDefibrillatedEvent>(OnDefibrillated);
    }

    /// <summary>
    /// Дефибриллятор снимает остановку сердца (set_heartattack(FALSE)).
    /// </summary>
    private void OnDefibrillated(Entity<FishComplexCritComponent> ent, ref TargetDefibrillatedEvent args)
    {
        if (IsUndergoingCardiacArrest(ent))
        {
            TrySetHeartAttack(ent, false);
            _popup.PopupEntity(Loc.GetString("fish-crit-defib-arrhythmia-cleared"), ent, args.User, PopupType.Medium);
        }

        // После успешного подъёма из Dead в Critical — soft-crit встаёт сам через OnMobStateChanged.
        RemCompDeferred<FishCritUnconsciousComponent>(ent);
        SetLoseBreath(ent, TimeSpan.Zero);
    }
}
