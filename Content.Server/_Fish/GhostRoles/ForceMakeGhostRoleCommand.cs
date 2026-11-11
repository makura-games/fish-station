using Content.Server.Administration;
using Content.Server.Ghost.Roles.Raffles;
using Content.Shared.Administration;
using Content.Shared.Ghost.Roles.Raffles;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.GhostRoles;

/// <summary>
/// Админ-команда forceghostrole — принудительная ghost role по UID.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed partial class ForceMakeGhostRoleCommand : LocalizedEntityCommands
{
    [Dependency] private ForceMakeGhostRoleSystem _forceMake = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override string Command => "forceghostrole";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        // forceghostrole <uid> <name> <description> [rules]
        // forceghostrole <uid> <name> <description> <raffle proto|initial extend max> [rules]
        if (args.Length is < 3 or > 7)
        {
            shell.WriteLine(Loc.GetString("cmd-forceghostrole-help"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netEntity) ||
            !EntityManager.TryGetEntity(netEntity, out var uid) ||
            !EntityManager.EntityExists(uid))
        {
            shell.WriteLine(Loc.GetString("shell-could-not-find-entity-with-uid", ("uid", args[0])));
            return;
        }

        var name = args[1];
        var description = args[2];
        GhostRoleRaffleConfig? raffleConfig = null;
        string? rules = null;

        if (args.Length == 4)
        {
            if (_prototype.TryIndex<GhostRoleRaffleSettingsPrototype>(args[3], out var raffleProto))
                raffleConfig = new GhostRoleRaffleConfig(raffleProto.Settings);
            else
                rules = args[3];
        }
        else if (args.Length == 5)
        {
            if (!_prototype.TryIndex<GhostRoleRaffleSettingsPrototype>(args[3], out var raffleProto))
            {
                shell.WriteLine(Loc.GetString("cmd-forceghostrole-invalid-raffle", ("proto", args[3])));
                return;
            }

            raffleConfig = new GhostRoleRaffleConfig(raffleProto.Settings);
            rules = args[4];
        }
        else if (args.Length is 6 or 7)
        {
            if (!uint.TryParse(args[3], out var initial) ||
                !uint.TryParse(args[4], out var extends) ||
                !uint.TryParse(args[5], out var max) ||
                initial == 0 || max == 0)
            {
                shell.WriteLine(Loc.GetString("cmd-forceghostrole-invalid-duration"));
                return;
            }

            if (initial > max)
            {
                shell.WriteLine(Loc.GetString("cmd-forceghostrole-initial-gt-max"));
                return;
            }

            raffleConfig = new GhostRoleRaffleConfig(new GhostRoleRaffleSettings
            {
                InitialDuration = initial,
                JoinExtendsDurationBy = extends,
                MaxDuration = max,
            });

            if (args.Length == 7)
                rules = args[6];
        }

        if (!_forceMake.TryForceMakeGhostRole(
                uid.Value,
                name,
                description,
                rules,
                makeSentient: true,
                allowMovement: true,
                allowSpeech: true,
                ejectExistingMind: true,
                raffleConfig: raffleConfig))
        {
            shell.WriteLine(Loc.GetString("cmd-forceghostrole-failed", ("uid", uid)));
            return;
        }

        var entityName = EntityManager.GetComponent<MetaDataComponent>(uid.Value).EntityName;
        shell.WriteLine(Loc.GetString("cmd-forceghostrole-success", ("name", entityName), ("uid", uid.Value)));
    }
}
