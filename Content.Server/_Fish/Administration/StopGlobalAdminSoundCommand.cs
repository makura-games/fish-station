using Content.Server.Administration;
using Content.Server.Audio;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Network;
using System.Linq;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class StopGlobalAdminSoundCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public string Command => "stopglobaladminsound";
    public string Description => Loc.GetString("stop-global-admin-sound-command-description");
    public string Help => Loc.GetString("stop-global-admin-sound-command-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var soundSystem = _entManager.System<ServerGlobalSoundSystem>();

        if (args.Length == 0)
        {
            soundSystem.StopAllAdminSounds();
            shell.WriteLine(Loc.GetString("stop-global-admin-sound-all-stopped"));
            return;
        }

        if (args.Length == 1)
        {
            var username = args[0];
            if (!_playerManager.TryGetSessionByUsername(username, out var session))
            {
                shell.WriteError(Loc.GetString("stop-global-admin-sound-player-not-found", ("username", username)));
                return;
            }

            soundSystem.StopPlayerAdminSounds(session.UserId);
            shell.WriteLine(Loc.GetString("stop-global-admin-sound-player-stopped", ("username", username)));
            return;
        }

        shell.WriteError(Loc.GetString("stop-global-admin-sound-usage"));
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = _playerManager.Sessions.Select(s => s.Name).ToList();
            return CompletionResult.FromHintOptions(options, Loc.GetString("stop-global-admin-sound-arg-player"));
        }

        return CompletionResult.Empty;
    }
}
