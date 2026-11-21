using Content.Server.Administration;
using Content.Shared._Fish.PerformanceMetrics;
using Content.Shared.Administration;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server._Fish.PerformanceMetrics;

/// <summary>
/// perfmon [ник] — сводка метрик по сессии.
/// perfmon config — текущие настройки наблюдения (значения серверные, в клиентском
/// списке cvar их нет намеренно).
/// perfmon reset &lt;ник&gt; — забыть накопленное по сессии.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed partial class PmStatusCommand : LocalizedEntityCommands
{
    [Dependency] private PmSystem _pm = default!;
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override string Command => "perfmon";
    public override string Help => "Usage: perfmon [nick] | config | reset <nick>";

    public override string Description =>
        "Метрики по игровым сессиям: сводка, настройки и сброс накопленного.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            WriteAll(shell);
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "config":
                WriteConfig(shell);
                return;
            case "reset" when args.Length == 2:
                Reset(shell, args[1]);
                return;
        }

        if (!TrySession(shell, args[0], out var session))
            return;

        if (!_pm.TryGetSession(session.UserId, out var pm))
        {
            shell.WriteLine("no metrics for this session yet");
            return;
        }

        shell.WriteLine($"metrics: {_pm.Describe(pm)}");
        shell.WriteLine($"motion strikes={pm.MotionStrikes} perfect hits={pm.PerfectHits} " +
                        $"flicks={pm.FlickStrikes} reach={pm.ReachStrikes} rate windows={pm.ActionStrikes}");
        shell.WriteLine($"line-of-sight strikes={pm.LosStrikes} ratio windows={pm.RatioStrikes}");
        shell.WriteLine($"shots sampled={pm.Attempts.Count} realised={Realised(pm)}");
        shell.WriteLine($"hit gaps={pm.HitGaps.Count} last={LastGap(pm)}");
    }

    private void WriteAll(IConsoleShell shell)
    {
        var any = false;
        foreach (var session in _pm.Sessions)
        {
            if (session.TotalFlags == 0)
                continue;

            any = true;
            var name = _players.SessionsDict.TryGetValue(session.UserId, out var player)
                ? player.Name
                : session.UserId.ToString();

            shell.WriteLine($"{name}: {_pm.Describe(session)}");
        }

        if (!any)
            shell.WriteLine("no sessions with flags");
    }

    private void WriteConfig(IConsoleShell shell)
    {
        shell.WriteLine($"perfmon.enabled={_cfg.GetCVar(PmCVars.Enabled)} " +
                        $"karma={_cfg.GetCVar(PmCVars.Karma)} " +
                        $"disconnect={_cfg.GetCVar(PmCVars.Disconnect)} " +
                        $"report={_cfg.GetCVar(PmCVars.Report)}");

        shell.WriteLine($"thresholds: aim<= {PmTuning.AimPerfectDegrees} deg over {PmTuning.AimStrikeCount} hits, " +
                        $"cadence CV < {PmTuning.CadenceRegularity}, flick > {PmTuning.FlickDegreesPerTick} deg, " +
                        $"speed margin {PmTuning.SpeedMargin}x over {PmTuning.MotionStrikesBeforeFlag} samples");

        shell.WriteLine($"reactions: karma -{PmTuning.KarmaPerFlag:0.##} per flag every " +
                        $"{PmTuning.KarmaCooldown.TotalMinutes:0} min, disconnect from {PmTuning.FlagsForDisconnect} flags");
    }

    private void Reset(IConsoleShell shell, string nick)
    {
        if (!TrySession(shell, nick, out var session))
            return;

        _pm.ResetSession(session.UserId);
        shell.WriteLine($"metrics forgotten for {session.Name}");
    }

    private bool TrySession(IConsoleShell shell, string nick, out ICommonSession session)
    {
        session = default!;

        foreach (var candidate in _players.Sessions)
        {
            if (!string.Equals(candidate.Name, nick, StringComparison.OrdinalIgnoreCase))
                continue;

            session = candidate;
            return true;
        }

        shell.WriteError($"Session '{nick}' not found.");
        return false;
    }

    private static string Realised(PmSession session)
    {
        var share = PmMaths.RealisedShare(session.Attempts, 30, Math.Min(PmTuning.RatioSampleCount, session.Attempts.Count));
        return share < 0f ? "not enough samples" : $"{share:P0}";
    }

    private static string LastGap(PmSession session)
    {
        return session.HitGaps.Count == 0
            ? "none"
            : $"{session.HitGaps[^1]:0.###}s (variation {PmMaths.VariationCoefficient(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(session.HitGaps)):0.####})";
    }
}
