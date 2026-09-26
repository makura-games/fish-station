using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Fish.GhostRoles;

/// <summary>
/// Пункт контекстного меню Admin: «Принудительная гост роль».
/// Подписка на GetVerbsEvent — без правок AdminVerbSystem.
/// </summary>
public sealed partial class ForceMakeGhostRoleVerbSystem : EntitySystem
{
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private ForceMakeGhostRoleSystem _forceMake = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        var player = actor.PlayerSession;
        if (!_admins.IsAdmin(player) || !_admins.HasAdminFlag(player, AdminFlags.Admin))
            return;

        if (args.User == args.Target)
            return;

        if (TerminatingOrDeleted(args.Target))
            return;

        var target = args.Target;
        var text = Loc.GetString("force-ghost-role-verb-get-data-text");

        args.Verbs.Add(new Verb
        {
            Text = text,
            Message = Loc.GetString("force-ghost-role-verb-get-data-desc"),
            Category = VerbCategory.Admin,
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/sentient.svg.192dpi.png")),
            Priority = 1,
            Impact = LogImpact.Medium,
            Act = () =>
            {
                var name = MetaData(target).EntityName;
                var description = Loc.GetString("force-ghost-role-verb-default-description", ("name", name));
                _forceMake.TryForceMakeGhostRole(
                    target,
                    name,
                    description,
                    rules: null,
                    makeSentient: true,
                    allowMovement: true,
                    allowSpeech: true,
                    ejectExistingMind: true);
            },
        });
    }
}
