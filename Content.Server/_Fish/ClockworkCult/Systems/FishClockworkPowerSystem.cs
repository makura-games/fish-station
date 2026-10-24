using Content.Server._Fish.ClockworkCult.GameRule;
using Content.Server.Chat.Systems;
using Content.Shared._Fish.ClockworkCult;
using Content.Shared._Fish.ClockworkCult.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Player;

namespace Content.Server._Fish.ClockworkCult.Systems;

/// <summary>
/// Управление глобальным пулом мощности и vitality Clockwork Cult.
/// </summary>
public sealed partial class FishClockworkPowerSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;

    public EntityUid? TryGetRuleEntity()
    {
        var query = EntityQueryEnumerator<FishClockworkCultRuleComponent, FishClockworkPowerComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out var uid, out _, out _, out _))
            return uid;

        return null;
    }

    public bool TryGetPower(out Entity<FishClockworkPowerComponent> power)
    {
        power = default;
        var rule = TryGetRuleEntity();
        if (rule == null || !TryComp(rule.Value, out FishClockworkPowerComponent? comp))
            return false;

        power = (rule.Value, comp);
        return true;
    }

    public float GetPower()
    {
        return TryGetPower(out var power) ? power.Comp.Power : 0f;
    }

    public float GetVitality()
    {
        return TryGetPower(out var power) ? power.Comp.Vitality : 0f;
    }

    /// <summary>
    /// Изменение мощности. Отрицательные значения — трата (Herald 0.75, Ratvar infinity).
    /// </summary>
    public bool TryAdjustPower(float amount, bool quiet = false)
    {
        if (!TryGetPower(out var power))
            return false;

        if (power.Comp.RatvarAwakened)
        {
            if (amount < 0)
                return true;

            power.Comp.Power = float.MaxValue;
            Dirty(power);
            return true;
        }

        var adjusted = amount;
        if (amount < 0 && power.Comp.Heralded)
            adjusted *= FishClockworkCultConstants.HeraldCostMultiplier;

        if (adjusted < 0 && power.Comp.Power + adjusted < 0)
            return false;

        power.Comp.Power = MathF.Max(0f, power.Comp.Power + adjusted);
        CheckUnlocks(power, quiet);
        Dirty(power);
        return true;
    }

    public bool TrySpendPower(float cost, bool quiet = false)
    {
        if (cost <= 0)
            return true;

        return TryAdjustPower(-cost, quiet);
    }

    public bool HasPower(float cost)
    {
        if (!TryGetPower(out var power))
            return false;

        if (power.Comp.RatvarAwakened)
            return true;

        var needed = cost;
        if (power.Comp.Heralded)
            needed *= FishClockworkCultConstants.HeraldCostMultiplier;

        return power.Comp.Power >= needed;
    }

    public void AdjustVitality(float amount)
    {
        if (!TryGetPower(out var power))
            return;

        power.Comp.Vitality = MathF.Max(0f, power.Comp.Vitality + amount);
        Dirty(power);
    }

    public bool TrySpendVitality(float cost)
    {
        if (!TryGetPower(out var power))
            return false;

        if (power.Comp.Vitality < cost)
            return false;

        power.Comp.Vitality -= cost;
        Dirty(power);
        return true;
    }

    public bool IsHeralded()
    {
        return TryGetPower(out var power) && power.Comp.Heralded;
    }

    public void SetHeralded(bool value)
    {
        if (!TryGetPower(out var power))
            return;

        power.Comp.Heralded = value;
        Dirty(power);
    }

    public void SetRatvarAwakened(bool value)
    {
        if (!TryGetPower(out var power))
            return;

        power.Comp.RatvarAwakened = value;
        if (value)
            power.Comp.Power = float.MaxValue;

        Dirty(power);
    }

    public void UnlockScript(bool forceAnnounce = true)
    {
        if (!TryGetPower(out var power) || power.Comp.ScriptUnlocked)
            return;

        power.Comp.ScriptUnlocked = true;
        Dirty(power);
        if (forceAnnounce)
            HierophantAnnounce(Loc.GetString("fish-clockwork-unlock-script"));
    }

    public void UnlockApplication(bool forceAnnounce = true)
    {
        if (!TryGetPower(out var power) || power.Comp.ApplicationUnlocked)
            return;

        power.Comp.ApplicationUnlocked = true;
        Dirty(power);
        if (forceAnnounce)
            HierophantAnnounce(Loc.GetString("fish-clockwork-unlock-application"));
    }

    private void CheckUnlocks(Entity<FishClockworkPowerComponent> power, bool quiet)
    {
        if (power.Comp.Power >= FishClockworkCultConstants.ScriptUnlockThreshold && !power.Comp.ScriptUnlocked)
        {
            power.Comp.ScriptUnlocked = true;
            if (!quiet)
                HierophantAnnounce(Loc.GetString("fish-clockwork-unlock-script"));
        }

        if (power.Comp.Power >= FishClockworkCultConstants.ApplicationUnlockThreshold && !power.Comp.ApplicationUnlocked)
        {
            power.Comp.ApplicationUnlocked = true;
            if (!quiet)
                HierophantAnnounce(Loc.GetString("fish-clockwork-unlock-application"));
        }
    }

    public void HierophantAnnounce(string message)
    {
        var filter = Filter.Empty();
        var query = EntityQueryEnumerator<FishClockworkServantComponent, ActorComponent>();
        while (query.MoveNext(out _, out _, out var actor))
        {
            filter.AddPlayer(actor.PlayerSession);
        }

        _chat.DispatchFilteredAnnouncement(
            filter,
            message,
            sender: Loc.GetString("fish-clockwork-hierophant"),
            playDefault: true,
            colorOverride: Color.FromHex("#BE8700"));
    }
}

