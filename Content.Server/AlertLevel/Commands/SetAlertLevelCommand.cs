using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.AlertLevel;
using Content.Shared.Station;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.AlertLevel.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class SetAlertLevelCommand : LocalizedEntityCommands
{
    [Dependency] private AlertLevelSystem _alertLevelSystem = default!;
    [Dependency] private SharedStationSystem _stationSystem = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override string Command => "setalertlevel";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<AlertLevelPrototype>(),
            Loc.GetString("cmd-setalertlevel-hint-1")),
            2 => CompletionResult.FromHintOptions(CompletionHelper.Booleans,
                LocalizationManager.GetString("cmd-setalertlevel-hint-2")),
            _ => CompletionResult.Empty,
        };
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var player = shell.Player;
        if (player?.AttachedEntity == null)
        {
            shell.WriteLine(LocalizationManager.GetString("shell-only-players-can-run-this-command"));
            return;
        }

        if (args.Length is < 1 or > 2) // Sunrise-Edit
        // if (args.Length < 1)
        {
            shell.WriteError(LocalizationManager.GetString("shell-wrong-arguments-number"));
            return;
        }

        bool? locked = null;
        if (args.Length > 1)
        {
            if (bool.TryParse(args[1], out var parsedBool))
            {
                locked = parsedBool;
            }
            else
            {
                shell.WriteLine(LocalizationManager.GetString("shell-argument-must-be-boolean"));
                return;
            }
        }

        var stationUid = _stationSystem.GetOwningStation(player.AttachedEntity.Value);
        if (stationUid == null)
        {
            shell.WriteLine(LocalizationManager.GetString("cmd-setalertlevel-invalid-grid"));
            return;
        }

        var level = args[0];
        // Sunrise edit start - второй параметр управляет состоянием дополнительного кода
        // if (!_prototype.HasIndex<AlertLevelPrototype>(level))
        if (!EntityManager.TryGetComponent<AlertLevelComponent>(stationUid.Value, out var alertLevelComp)
            || !alertLevelComp.AvailableAlertLevels.Contains(level)
            || !_prototype.TryIndex<AlertLevelPrototype>(level, out var detail))
        // Sunrise edit end
        {
            shell.WriteLine(LocalizationManager.GetString("cmd-setalertlevel-invalid-level"));
            return;
        }

        // Sunrise added start - второй параметр управляет состоянием дополнительного кода
        if (detail.IsAdditional)
        {
            var enabled = locked ?? true;
            if (!_alertLevelSystem.TrySetAdditionalLevel(
                    (stationUid.Value, alertLevelComp),
                    level,
                    enabled,
                    playSound: true,
                    announce: true,
                    force: true))
            {
                shell.WriteError(LocalizationManager.GetString("cmd-setalertlevel-change-failed"));
            }

            return;
        }
        // Sunrise added end

        _alertLevelSystem.SetLevel(stationUid.Value, level, force: true, locked: locked);
    }
}
