using System.Globalization;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared._Fish.Rpu;
using Robust.Shared.Console;
using Robust.Shared.Player;

using System.Linq;
using Robust.Shared.Enums;
using Robust.Shared.Audio.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Gibbing;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Throwing;
using Content.Shared.Wires;
using Content.Shared.Slippery;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;
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

            // Прогноз по тем же функциям, что и наказание: сверить расчёт можно
            // не перезапуская раунд.
            var limit = _rpu.CurrentBanThreshold(session);
            var crew = _rpu.PreviewPenalty(session, RpuPrototypeIds.KillCrew);

            if (float.IsNegativeInfinity(limit))
                shell.WriteLine("ban limit: none for this player");
            else
            {
                // До порога осталось столько, насколько карма выше предела: знак здесь
                // важен, иначе у игрока с +50 и предела -40 выходило «-10 очков».
                var remaining = Math.Max(0f, suspect.Karma - limit);
                var until = crew > 0f ? (int)Math.Ceiling(remaining / crew) : 0;
                shell.WriteLine($"ban limit: {limit:0.##}  crewKill cost: {crew:0.##}  " +
                                $"until limit: {remaining:0.##} points (~{Math.Max(0, until)} crew kills)");
            }

            var securityChain = _rpu.PreviewPenalty(session, RpuPrototypeIds.KillSecurityChain);
            shell.WriteLine($"securityChainKill cost: {securityChain:0.##}");

            var (lost, spikeUnits, spikeMinutes) = _rpu.SpikeInfo(session);
            var spike = spikeUnits > 0f ? spikeUnits.ToString("0.##") : "off";
            shell.WriteLine($"loss in last {spikeMinutes} min: {lost:0.##} (instant restriction above {spike})");
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
