using System.Globalization;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared._Fish.Rpu;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// rpu_karma &lt;ник&gt; [set &lt;значение&gt; | add &lt;дельта&gt; | clear]
/// Просмотр и правка кармы: остальное система делает сама.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed partial class RpuKarmaCommand : LocalizedEntityCommands
{
    [Dependency] private RpuSystem _rpu = default!;
    [Dependency] private ISharedPlayerManager _players = default!;

    public override string Command => "rpu_karma";
    public override string Help => "Usage: rpu_karma <nick> [set <value> | add <delta> | clear]";
    public override string Description => "Просмотр и правка кармы режима персонального угнетения.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (!TryResolve(args[0], out var session))
        {
            shell.WriteError($"Player '{args[0]}' not found.");
            return;
        }

        var suspect = _rpu.GetSuspect(session.UserId);

        if (args.Length == 1)
        {
            shell.WriteLine($"karma={suspect.Karma:0.##} base={suspect.BaseKarma:0.##} " +
                            $"roleHours={suspect.RoleHours:0.#} kills={suspect.KillsThisRound} " +
                            $"recentBans={suspect.RecentBans} exempt={_rpu.IsExempt(session)}");
            return;
        }

        var adjustment = 0f;

        switch (args[1].ToLowerInvariant())
        {
            case "clear":
                adjustment = -suspect.Karma;
                break;
            case "add" when args.Length == 3 && TryNumber(args[2], out var delta):
                adjustment = delta;
                break;
            case "set" when args.Length == 3 && TryNumber(args[2], out var value):
                adjustment = value - suspect.Karma;
                break;
            default:
                shell.WriteError(Help);
                return;
        }

        if (!_rpu.TryAdjustKarma(session, adjustment))
        {
            shell.WriteError("Unable to adjust karma.");
            return;
        }

        shell.WriteLine($"karma={suspect.Karma:0.##}");
    }

    /// <summary>
    /// Число для правки кармы. NaN и бесконечность culture-dependent парсятся как валидные
    /// значения, а карму потом не вернуть в диапазон простыми штрафами.
    /// </summary>
    private static bool TryNumber(string text, out float value)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && float.IsFinite(value);
    }

    private bool TryResolve(string name, out ICommonSession session)
    {
        foreach (var player in _players.Sessions)
        {
            if (!string.Equals(player.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;

            session = player;
            return true;
        }

        session = default!;
        return false;
    }
}
