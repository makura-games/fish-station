using Content.Server.Ghost.Roles.Components;
using Content.Server.StationEvents.Components;
using Content.Shared._Fish.Xenobiology.Extracts;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;

namespace Content.Server._Fish.Xenobiology.Extracts;

public sealed partial class FishSlimePotionSystem
{
    [Dependency] private SharedMindSystem _mind = default!;

    private void OnSentienceTargetInteract(Entity<SentienceTargetComponent> target, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<FishSlimePotionComponent>(args.Used, out var potion))
            return;

        if (potion.Kind is not (FishSlimePotionKind.Sentience or FishSlimePotionKind.Transference))
            return;

        if (!_mobState.IsAlive(target))
        {
            _popup.PopupEntity(Loc.GetString("fish-slime-potion-dead"), args.User, args.User);
            return;
        }

        if (_mind.TryGetMind(args.User, out _, out _))
        {
            if (potion.Kind == FishSlimePotionKind.Transference)
            {
                args.Handled = TryTransference(args.User, target, args.Used);
                return;
            }
        }

        if (potion.Kind == FishSlimePotionKind.Sentience)
            args.Handled = TrySentience(target, args.User, args.Used);
    }

    private bool TrySentience(Entity<SentienceTargetComponent> target, EntityUid user, EntityUid potion)
    {
        if (_mind.TryGetMind(target, out _, out _))
        {
            _popup.PopupEntity(Loc.GetString("fish-slime-potion-sentience-has-mind"), user, user);
            return false;
        }

        RemComp<SentienceTargetComponent>(target);
        var ghostRole = EnsureComp<GhostRoleComponent>(target);
        EnsureComp<GhostTakeoverAvailableComponent>(target);
        ghostRole.RoleName = MetaData(target).EntityName;
        ghostRole.RoleDescription = Loc.GetString("fish-slime-potion-sentience-role-desc", ("name", ghostRole.RoleName));

        _popup.PopupEntity(Loc.GetString("fish-slime-potion-sentience"), target, user);
        QueueDel(potion);
        return true;
    }

    private bool TryTransference(EntityUid user, EntityUid target, EntityUid potion)
    {
        if (!_mind.TryGetMind(user, out var mindId, out var mind))
        {
            _popup.PopupEntity(Loc.GetString("fish-slime-potion-transference-no-mind"), user, user);
            return false;
        }

        if (_mind.TryGetMind(target, out _, out _))
        {
            _popup.PopupEntity(Loc.GetString("fish-slime-potion-sentience-has-mind"), user, user);
            return false;
        }

        if (!HasComp<SentienceTargetComponent>(target) && !HasComp<FishSlimeComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("fish-slime-potion-transference-invalid"), user, user);
            return false;
        }

        var oldBody = mind.CurrentEntity;
        _mind.TransferTo(mindId, target, mind: mind);
        _popup.PopupEntity(Loc.GetString("fish-slime-potion-transference"), target, user);
        QueueDel(potion);

        if (oldBody is { } body && body != target && TryComp<MobStateComponent>(body, out _))
            _mobState.ChangeMobState(body, MobState.Dead);

        return true;
    }
}
