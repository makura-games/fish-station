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
        var query = EntityQueryEnumerator<PlanetWarRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var rule, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(ruleUid, gameRule))
                continue;

            if (rule.Ending)
                return;

            // Уничтожены врата команды → побеждает противоположная.
            var winner = ent.Comp.Team switch
            {
                PlanetWarTeam.Core => PlanetWarTeam.Arm,
                PlanetWarTeam.Arm => PlanetWarTeam.Core,
                _ => (PlanetWarTeam?) null,
            };

            if (winner == null)
                return;

            EndPlanetWar(rule, winner.Value);
            return;
        }
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
