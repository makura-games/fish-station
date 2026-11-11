using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server._Fish.RoundEnd
{
    [AdminCommand(AdminFlags.Admin)]
    public sealed partial class LobbyPopupToggleCommand : LocalizedCommands
    {
        [Dependency] private IConfigurationManager _configManager = default!;

        public override string Command => "lobbypopuptoggle";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 1)
            {
                shell.WriteLine("Usage: lobbypopuptoggle <true/false>");
                return;
            }

            if (bool.TryParse(args[0], out var result))
            {
                _configManager.SetCVar(FishCVars.EorgPopupEnabled, result);
                shell.WriteLine($"Lobby popup has been {(result ? "enabled" : "disabled")}.");
            }
            else
            {
                shell.WriteLine("Invalid boolean value. Must be 'true' or 'false'.");
            }
        }
    }
}
