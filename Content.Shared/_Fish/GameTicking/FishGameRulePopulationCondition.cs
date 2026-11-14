using Content.Shared.EntityTable;
using Content.Shared.EntityTable.Conditions;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.GameTicking;

/// <summary>
/// Проверяет минимальное число игроков из прототипа игрового правила.
/// </summary>
public sealed partial class FishGameRulePopulationCondition : EntityTableCondition
{
    public const string ReadyPlayersContextKey = "FishReadyPlayers";

    protected override bool EvaluateImplementation(
        EntityTableSelector root,
        IEntityManager entMan,
        IPrototypeManager proto,
        EntityTableContext ctx)
    {
        if (root is not EntSelector selector ||
            !proto.Index(selector.Id).TryComp<GameRuleComponent>(out var rule, entMan.ComponentFactory))
            return true;

        return ctx.TryGetData<int>(ReadyPlayersContextKey, out var readyPlayers) &&
               readyPlayers >= rule.MinPlayers;
    }
}
