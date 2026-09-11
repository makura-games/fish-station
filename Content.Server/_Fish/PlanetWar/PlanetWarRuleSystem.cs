using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._Fish.PlanetWar;
using Content.Shared.GameTicking.Components;
using Content.Shared.Trigger;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Fish.PlanetWar;

/// <summary>
/// Sunrise-style GameRule для PlanetWar: старт объявления, победа по уничтожению врат, текст конца раунда.
/// </summary>
public sealed class PlanetWarRuleSystem : GameRuleSystem<PlanetWarRuleComponent>
{
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlanetWarGatewayComponent, TriggerEvent>(OnGatewayTriggered);
    }

    protected override void Started(
        EntityUid uid,
        PlanetWarRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("planetwar-round-start-announcement"),
            Loc.GetString("planetwar-announcer"),
            playDefault: true,
            colorOverride: Color.Gold);
    }

    protected override void AppendRoundEndText(
        EntityUid uid,
        PlanetWarRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        var line = component.Winner switch
        {
            PlanetWarTeam.Core => Loc.GetString("planetwar-round-end-winner-core"),
            PlanetWarTeam.Arm => Loc.GetString("planetwar-round-end-winner-arm"),
            _ => Loc.GetString("planetwar-round-end-stalemate"),
        };

        args.AddLine(line);
    }

    private void OnGatewayTriggered(Entity<PlanetWarGatewayComponent> ent, ref TriggerEvent args)
    {
        if (args.Handled)
            return;

        if (TryEndPlanetWar(ent))
            args.Handled = true;
    }

    /// <summary>
    /// Попытка завершить PlanetWar при уничтожении врат.
    /// </summary>
    public bool TryEndPlanetWar(Entity<PlanetWarGatewayComponent> gateway)
    {
        if (!CanEndPlanetWar(gateway, out var targetRule, out var winner))
            return false;

        EndPlanetWar(targetRule, winner.Value);
        return true;
    }

    /// <summary>
    /// Проверка возможности завершения PlanetWar при уничтожении врат.
    /// </summary>
    public bool CanEndPlanetWar(
        Entity<PlanetWarGatewayComponent> gateway,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PlanetWarRuleComponent? targetRule,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PlanetWarTeam? winner)
    {
        targetRule = null;
        winner = null;

        var calculatedWinner = gateway.Comp.Team switch
        {
            PlanetWarTeam.Core => PlanetWarTeam.Arm,
            PlanetWarTeam.Arm => PlanetWarTeam.Core,
            _ => (PlanetWarTeam?) null,
        };

        if (calculatedWinner == null)
            return false;

        var query = EntityQueryEnumerator<PlanetWarRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var rule, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(ruleUid, gameRule))
                continue;

            if (rule.Ending)
                continue;

            targetRule = rule;
            winner = calculatedWinner;
            return true;
        }

        return false;
    }

    private void EndPlanetWar(PlanetWarRuleComponent rule, PlanetWarTeam winner)
    {
        rule.Ending = true;
        rule.Winner = winner;

        var announce = winner switch
        {
            PlanetWarTeam.Core => Loc.GetString("planetwar-gateway-destroyed-arm"),
            PlanetWarTeam.Arm => Loc.GetString("planetwar-gateway-destroyed-core"),
            _ => Loc.GetString("planetwar-round-end-stalemate"),
        };

        var endText = winner switch
        {
            PlanetWarTeam.Core => Loc.GetString("planetwar-round-end-winner-core"),
            PlanetWarTeam.Arm => Loc.GetString("planetwar-round-end-winner-arm"),
            _ => Loc.GetString("planetwar-round-end-stalemate"),
        };

        _chat.DispatchGlobalAnnouncement(
            announce,
            Loc.GetString("planetwar-announcer"),
            playDefault: true,
            colorOverride: Color.OrangeRed);

        GameTicker.EndRound(endText);

        var delay = rule.RoundEndDelay;
        Timer.Spawn(delay, () => GameTicker.RestartRound());
    }
}
