using Content.Server.Explosion.EntitySystems;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Raffles;
using Content.Server.StationEvents.Components;
using Content.Shared._Fish.Xenobiology.Extracts;
using Content.Shared.Ghost.Roles.Raffles;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Xenobiology.Extracts;

public sealed partial class FishSlimePotionSystem
{
    private static readonly ProtoId<GhostRoleRaffleSettingsPrototype> SentienceRaffle = "FishXenoSentienceRaffleFish";

    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

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
            HeatSentiencePotion(potion, user);
            _popup.PopupEntity(Loc.GetString("fish-slime-potion-sentience-has-mind"), user, user);
            return false;
        }

        if (TryComp<FishSlimeSentiencePotionComponent>(potion, out var warm) && warm.WarmStage >= 2)
        {
            DetonateSentiencePotion(potion, user);
            return true;
        }

        RemComp<SentienceTargetComponent>(target);
        var ghostRole = EnsureComp<GhostRoleComponent>(target);
        EnsureComp<GhostTakeoverAvailableComponent>(target);
        ghostRole.RoleName = MetaData(target).EntityName;
        ghostRole.RoleDescription = Loc.GetString("fish-slime-potion-sentience-role-desc", ("name", ghostRole.RoleName));
        ghostRole.RoleRules = "ghost-role-component-default-rules";
        var raffleSettings = _proto.Index(SentienceRaffle).Settings;
        ghostRole.RaffleConfig = new GhostRoleRaffleConfig(raffleSettings);

        _popup.PopupEntity(Loc.GetString("fish-slime-potion-sentience-raffle"), target, user);
        QueueDel(potion);
        return true;
    }

    private void HeatSentiencePotion(EntityUid potion, EntityUid user)
    {
        var warm = EnsureComp<FishSlimeSentiencePotionComponent>(potion);
        warm.WarmStage++;
        Dirty(potion, warm);

        if (warm.WarmStage >= 3)
            DetonateSentiencePotion(potion, user);
        else
            _popup.PopupEntity(Loc.GetString("fish-slime-potion-sentience-warm", ("stage", warm.WarmStage)), user, user);
    }

    private void DetonateSentiencePotion(EntityUid potion, EntityUid user)
    {
        var coords = _transform.GetMapCoordinates(potion);
        _explosion.QueueExplosion(potion, "Default", 50f, 1f, 0.5f, canCreateVacuum: false, user: user);
        QueueDel(potion);
        _popup.PopupEntity(Loc.GetString("fish-slime-potion-sentience-boom"), user, user);
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
