using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server._Fish.RoundEnd
{
    [AdminCommand(AdminFlags.Admin)]
    public sealed partial class LobbyPopupSetMessageCommand : LocalizedCommands
    {
        [Dependency] private IConfigurationManager _configManager = default!;

        public override string Command => "lobbypopupsetmessage";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length == 0)
            {
                shell.WriteLine("Usage: lobbypopupsetmessage <message text>");
                return;
            }

            var message = string.Join(" ", args);
            _configManager.SetCVar(FishCVars.EorgPopupMessage, message);
            shell.WriteLine($"Lobby popup message updated.");
        }
    }
}
