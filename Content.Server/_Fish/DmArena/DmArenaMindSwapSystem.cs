using System;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Shared._Fish.DmArena;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Fish.DmArena;

/// <summary>
/// Временная подмена разума: настоящий разум игрока откладывается, а в тело арены идёт одноразовый.
/// После выхода одноразовый разум удаляется, и игрок возвращается к своему прежнему разуму.
/// </summary>
public sealed partial class DmArenaMindSwapSystem : EntitySystem
{
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private MindExamineSystem _mindExamine = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DmArenaMindSwapComponent, PlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerDetached(Entity<DmArenaMindSwapComponent> ent, ref PlayerDetachedEvent args)
    {
        if (args.Player.Status != SessionStatus.Disconnected)
            return;

        CleanupDisposableMind(ent, ent.Comp);
        RemComp<DmArenaMindSwapComponent>(ent);
    }

    public bool TrySwapTempMind(ICommonSession session, EntityUid newBody)
    {
        if (session.AttachedEntity is not { Valid: true } currentEntity
            || !_mind.TryGetMind(currentEntity, out var origMindId, out var origMind))
            return false;

        var userId = origMind.UserId;
        _mind.UnVisit(origMindId, origMind);

        // Освобождаем userId для одноразового разума
        _mind.SetUserId(origMindId, null, origMind);

        var newMind = _mind.CreateMind(userId, origMind.CharacterName);

        try
        {
            _mind.TransferTo(newMind, newBody);
            _playerManager.SetAttachedEntity(session, newBody);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to swap mind of {session.Name} into {ToPrettyString(newBody)}: {e}");
            QueueDel(newMind);
            _mind.SetUserId(origMindId, userId, origMind);
            return false;
        }

        var swap = EnsureComp<DmArenaMindSwapComponent>(newBody);
        swap.OriginalMind = origMindId;
        swap.DisposableMind = newMind;

        return true;
    }

    public EntityUid? TryRestoreAsGhost(EntityUid temporaryBody)
    {
        if (!TryComp<DmArenaMindSwapComponent>(temporaryBody, out var temp)
            || !TryComp<MindComponent>(temp.OriginalMind, out var origMind))
            return null;

        var coords = _transform.GetMapCoordinates(temporaryBody);

        CleanupDisposableMind(temporaryBody, temp);

        var ghost = Spawn("MobObserver", coords);
        _mind.Visit(temp.OriginalMind, ghost, origMind);

        if (TryComp<GhostComponent>(ghost, out var ghostComp))
            _ghost.SetCanReturnToBody((ghost, ghostComp), origMind.OwnedEntity is { } owned && Exists(owned));

        if (GetGhostName(origMind) is { } name)
            _meta.SetEntityName(ghost, FormattedMessage.EscapeText(name));

        RemComp<DmArenaMindSwapComponent>(temporaryBody);
        return ghost;
    }

    private string? GetGhostName(MindComponent mind)
    {
        if (!string.IsNullOrWhiteSpace(mind.CharacterName))
            return mind.CharacterName;

        return mind.UserId is { } userId && _playerManager.TryGetSessionById(userId, out var session)
            ? session.Name
            : null;
    }

    public bool TryRestoreToOriginalBody(EntityUid temporaryBody)
    {
        if (!TryComp<DmArenaMindSwapComponent>(temporaryBody, out var temp)
            || !TryComp<MindComponent>(temp.OriginalMind, out var origMind)
            || origMind.OwnedEntity is not { } originalBody
            || !Exists(originalBody))
            return false;

        CleanupDisposableMind(temporaryBody, temp);

        if (origMind.UserId is { } userId && _playerManager.TryGetSessionById(userId, out var session))
            _playerManager.SetAttachedEntity(session, originalBody);

        RemComp<DmArenaMindSwapComponent>(temporaryBody);
        return true;
    }

    private void CleanupDisposableMind(EntityUid temporaryBody, DmArenaMindSwapComponent temp)
    {
        if (TryComp<MindContainerComponent>(temporaryBody, out var tempContainer)
            && tempContainer.LastMindStored == temp.DisposableMind)
        {
            tempContainer.LastMindStored = null;
            Dirty(temporaryBody, tempContainer);
        }

        if (Exists(temp.DisposableMind))
        {
            _mind.WipeMind(temp.DisposableMind);
            QueueDel(temp.DisposableMind);
        }

        if (!TryComp<MindComponent>(temp.OriginalMind, out var origMind)
            || origMind.OriginalOwnerUserId is not { } userId)
            return;

        _mind.SetUserId(temp.OriginalMind, userId);

        if (origMind.OwnedEntity is { } originalBody)
            _mindExamine.RefreshMindStatus(originalBody);
    }
}
