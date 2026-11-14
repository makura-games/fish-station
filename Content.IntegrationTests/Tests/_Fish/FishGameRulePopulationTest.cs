using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._Fish.GameTicking;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.GameTicking.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish;

[TestFixture]
public sealed class FishGameRulePopulationTest : GameTest
{
    [Test]
    [RunOnSide(Side.Server)]
    public void PopulationConditionUsesMinimumFromRulePrototype()
    {
        var revolutionary = new EntSelector { Id = "Revolutionary" };
        var traitor = new EntSelector { Id = "Traitor" };
        var condition = new FishGameRulePopulationCondition();
        var context = new EntityTableContext();

        context.SetData(FishGameRulePopulationCondition.ReadyPlayersContextKey, 5);
        Assert.That(condition.Evaluate(revolutionary, SEntMan, SProtoMan, context), Is.False);
        Assert.That(condition.Evaluate(traitor, SEntMan, SProtoMan, context), Is.True);

        Assert.That(SProtoMan.Index(revolutionary.Id)
            .TryComp<GameRuleComponent>(out var rule, SEntMan.ComponentFactory), Is.True);
        context.SetData(FishGameRulePopulationCondition.ReadyPlayersContextKey, rule!.MinPlayers);
        Assert.That(condition.Evaluate(revolutionary, SEntMan, SProtoMan, context), Is.True);
    }
}
