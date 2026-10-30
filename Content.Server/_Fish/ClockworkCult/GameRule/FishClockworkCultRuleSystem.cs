using Content.Server._Fish.ClockworkCult.Systems;
using Content.Server.Antag;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.RoundEnd;
using Content.Shared._Fish.ClockworkCult;
using Content.Shared._Fish.ClockworkCult.Components;
using Content.Shared._Fish.ClockworkCult.Events;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Player;

namespace Content.Server._Fish.ClockworkCult.GameRule;

/// <summary>
/// Game rule Clockwork Cult: старт, трекинг слуг, win/loss по Ark / Ratvar.
/// </summary>
public sealed partial class FishClockworkCultRuleSystem : GameRuleSystem<FishClockworkCultRuleComponent>
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private FishClockworkPowerSystem _power = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private FishClockworkServantSystem _servant = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishClockworkCultRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagSelected);
        SubscribeLocalEvent<FishClockworkArkComponent, ComponentStartup>(OnArkStartup);
        SubscribeLocalEvent<FishClockworkCultEndedEvent>(OnCultEnded);
    }

    protected override void Started(
        EntityUid uid,
        FishClockworkCultRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var power = EnsureComp<FishClockworkPowerComponent>(uid);
        power.Power = 0f;
        power.Vitality = 0f;
        power.ScriptUnlocked = false;
        power.ApplicationUnlocked = false;
        power.Heralded = false;
        power.RatvarAwakened = false;
        Dirty(uid, power);

        var players = Math.Max(1, _player.PlayerCount);
        var arkMinutes = FishClockworkCultConstants.CalculateArkPrepMinutes(players);

        // Привязка таймера к уже заспавненному Ark (LoadMapRule грузит карту раньше).
        var arkQuery = EntityQueryEnumerator<FishClockworkArkComponent>();
        while (arkQuery.MoveNext(out var arkUid, out var ark))
        {
            component.ArkUid = arkUid;
            ark.SecondsUntilActivation = arkMinutes * 60f;
            ark.InitialActivationDelay = ark.SecondsUntilActivation;
            ark.Phase = FishClockworkArkPhase.Prep;
            Dirty(arkUid, ark);
            break;
        }

        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("fish-clockwork-round-start"),
            Loc.GetString("fish-clockwork-announcer"),
            playDefault: true,
            colorOverride: Color.FromHex("#BE8700"));

        _power.HierophantAnnounce(Loc.GetString("fish-clockwork-ark-prep", ("minutes", arkMinutes)));
    }

    protected override void AppendRoundEndText(
        EntityUid uid,
        FishClockworkCultRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        var line = component.WinCondition switch
        {
            FishClockworkWinCondition.CultWin => Loc.GetString("fish-clockwork-round-end-win"),
            FishClockworkWinCondition.CultFailure => Loc.GetString("fish-clockwork-round-end-loss"),
            _ => Loc.GetString("fish-clockwork-round-end-stalemate"),
        };

        args.AddLine(line);
        args.AddLine(Loc.GetString("fish-clockwork-round-end-servants", ("count", component.Servants.Count)));
    }

    private void OnAfterAntagSelected(Entity<FishClockworkCultRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        _servant.TryAddStartingServant(args.EntityUid);
    }

    private void OnArkStartup(Entity<FishClockworkArkComponent> ent, ref ComponentStartup args)
    {
        var query = EntityQueryEnumerator<FishClockworkCultRuleComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out _, out var rule, out _))
        {
            rule.ArkUid = ent.Owner;
            break;
        }
    }

    private void OnCultEnded(FishClockworkCultEndedEvent args)
    {
        var query = EntityQueryEnumerator<FishClockworkCultRuleComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out var uid, out var rule, out _))
        {
            if (rule.Ending)
                continue;

            EndCult(uid, rule, args.Condition);
            break;
        }
    }

    public void EndCult(EntityUid ruleUid, FishClockworkCultRuleComponent rule, FishClockworkWinCondition condition)
    {
        if (rule.Ending)
            return;

        rule.Ending = true;
        rule.WinCondition = condition;

        var announce = condition switch
        {
            FishClockworkWinCondition.CultWin => Loc.GetString("fish-clockwork-announce-win"),
            FishClockworkWinCondition.CultFailure => Loc.GetString("fish-clockwork-announce-loss"),
            _ => Loc.GetString("fish-clockwork-round-end-stalemate"),
        };

        _chat.DispatchGlobalAnnouncement(
            announce,
            Loc.GetString("fish-clockwork-announcer"),
            playDefault: true,
            colorOverride: Color.FromHex("#BE8700"));

        // Как Nukeops/BloodCult: RoundEndSystem завершает раунд и сам планирует restart
        // (без дублирования текста из AppendRoundEndText и без гонки Timer.Spawn + RestartRound).
        _roundEnd.EndRound(rule.RoundEndDelay);
    }

    public void NotifyArkDestroyed()
    {
        RaiseLocalEvent(new FishClockworkCultEndedEvent(FishClockworkWinCondition.CultFailure));
    }

    public void NotifyRatvarArrived()
    {
        _power.SetRatvarAwakened(true);
        RaiseLocalEvent(new FishClockworkCultEndedEvent(FishClockworkWinCondition.CultWin));
    }
}

