using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Fish.PerformanceGuardian;

/// <summary>
/// Admin command: hints the client to open Performance Guardian (requires Debug).
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed partial class PgAdminCommand : LocalizedEntityCommands
{
    [Dependency] private PerformanceGuardianSystem _guardian = default!;

    public override string Command => "perfguardian";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        _guardian.HintOpenWindow(player);
        shell.WriteLine(Loc.GetString("pg-cmd-opened"));
    }
}

[AdminCommand(AdminFlags.Debug)]
public sealed partial class PgAdminCommandAlias : LocalizedEntityCommands
{
    [Dependency] private PerformanceGuardianSystem _guardian = default!;

    public override string Command => "pg";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        _guardian.HintOpenWindow(player);
        shell.WriteLine(Loc.GetString("pg-cmd-opened"));
    }
}
