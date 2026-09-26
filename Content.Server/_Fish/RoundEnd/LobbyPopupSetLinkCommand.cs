using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server._Fish.RoundEnd
{
    [AdminCommand(AdminFlags.Admin)]
    public sealed partial class LobbyPopupSetLinkCommand : LocalizedCommands
    {
        [Dependency] private IConfigurationManager _configManager = default!;

        public override string Command => "lobbypopupsetlink";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 1)
            {
                shell.WriteLine("Usage: lobbypopupsetlink <link>");
                return;
            }

            var link = args[0];
            _configManager.SetCVar(FishCVars.EorgPopupLink, link);
            shell.WriteLine($"Lobby popup link updated to: {link}");
        }
    }
}
