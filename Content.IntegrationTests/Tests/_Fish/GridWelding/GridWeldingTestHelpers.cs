using Content.IntegrationTests.Pair;
using Content.Server._Fish.GridWelding;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Fish.GridWelding;

internal static class GridWeldingTestHelpers
{
    /// <summary>
    /// Waits for completion or cancellation using the created DoAfter's delay, which already includes tool speed.
    /// Rejected actions still receive a tick so their immediate and deferred effects can be checked.
    /// </summary>
    public static async Task WaitForWeldingAsync(TestPair pair, EntityUid welder)
    {
        var server = pair.Server;
        var entities = server.EntMan;
        var remainingTicks = 1;

        await server.WaitAssertion(() =>
        {
            if (entities.GetComponent<GridWeldingComponent>(welder).DoAfter is not { } id)
                return;

            var actions = entities.GetComponent<DoAfterComponent>(id.Uid).DoAfters;
            Assert.That(actions.ContainsKey(id.Index), Is.True, "The welder must reference an existing DoAfter.");
            var delay = actions[id.Index].Args.Delay;
            Assert.That(delay, Is.GreaterThan(TimeSpan.Zero).And.LessThanOrEqualTo(TimeSpan.FromSeconds(30)),
                "The welding DoAfter must have a finite, bounded test duration.");

            // Задержка уже учитывает SpeedModifier; два тика оставляем на Update и обработку завершения.
            const int completionTickAllowance = 2;
            remainingTicks = (int) Math.Ceiling(delay / server.Timing.TickPeriod) + completionTickAllowance;
        });

        var finished = false;
        while (remainingTicks-- > 0)
        {
            await pair.RunTicksSync(1);
            await server.WaitAssertion(() =>
            {
                var selection = entities.GetComponent<GridWeldingComponent>(welder);
                finished = selection.DoAfter == null && selection.PendingWeld == null;
            });
            if (finished)
                return;
        }

        Assert.That(finished, Is.True, "Welding did not complete or cancel within its DoAfter delay and tick allowance.");
    }
}
