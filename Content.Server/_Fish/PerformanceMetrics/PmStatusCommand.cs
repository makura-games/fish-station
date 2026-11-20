using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server._Fish.PerformanceMetrics;

/// <summary>
/// perfmon &lt;ник&gt; — сводка метрик по подключению. Просмотр ничего не меняет
/// и наружу не влияет.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed partial class PmStatusCommand : LocalizedEntityCommands
{
    [Dependency] private PmSystem _pm = default!;
    [Dependency] private ISharedPlayerManager _players = default!;

    public override string Command => "perfmon";
    public override string Help => "Usage: perfmon <nick>";
    public override string Description => "Сводка метрик по игровой сессии.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        ICommonSession? found = null;
        foreach (var session in _players.Sessions)
        {
            if (!string.Equals(session.Name, args[0], StringComparison.OrdinalIgnoreCase))
                continue;

            found = session;
            break;
        }

        if (found == null)
        {
            shell.WriteError($"Session '{args[0]}' not found.");
            return;
        }

        if (!_pm.TryGetSession(found.UserId, out var pm))
        {
            shell.WriteLine("no metrics for this session yet");
            return;
        }

        shell.WriteLine($"metrics: {_pm.Describe(pm)}");
        shell.WriteLine($"motion strikes={pm.MotionStrikes} perfect hits={pm.PerfectHits} " +
                        $"flicks={pm.FlickStrikes} reach={pm.ReachStrikes} rate windows={pm.ActionStrikes}");
        shell.WriteLine($"hit gaps={pm.HitGaps.Count} last={LastGap(pm)}");
    }

    private static string LastGap(PmSession session)
    {
        return session.HitGaps.Count == 0
            ? "none"
            : $"{session.HitGaps[^1]:0.###}s (variation {PmMaths.VariationCoefficient(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(session.HitGaps)):0.####})";
    }
}
