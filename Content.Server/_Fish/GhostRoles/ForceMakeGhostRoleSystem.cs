using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Raffles;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;

namespace Content.Server._Fish.GhostRoles;

/// <summary>
/// Принудительное создание ghost role без правок MindSystem / GhostRoleSystem.
/// Только публичные API; поля GhostRole с ограниченным Access не трогаем.
/// </summary>
public sealed partial class ForceMakeGhostRoleSystem : EntitySystem
{
    [Dependency] private GhostRoleSystem _ghostRoles = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    /// <summary>
    /// Форсирует ghost role на сущности. Работает без Mind, с пустым/устаревшим MindContainer,
    /// на NPC и объектах. При живом разуме — отсоединяет его (если разрешено).
    /// </summary>
    public bool TryForceMakeGhostRole(
        EntityUid uid,
        string name,
        string description,
        string? rules = null,
        bool makeSentient = true,
        bool allowMovement = true,
        bool allowSpeech = true,
        bool ejectExistingMind = true,
        GhostRoleRaffleConfig? raffleConfig = null)
    {
        if (TerminatingOrDeleted(uid))
            return false;

        // Живой разум — отсоединяем через публичный TransferTo.
        if (_mind.TryGetMind(uid, out var mindId, out var mind))
        {
            if (!ejectExistingMind)
                return false;

            _mind.TransferTo(mindId, null, createGhost: mind.UserId != null, mind: mind);
        }
        else if (TryComp(uid, out MindContainerComponent? container) && container.Mind != null)
        {
            // Устаревший UID в MindContainer: HasMind == true, но MindComponent уже нет.
            // Поле Mind пишет только SharedMindSystem — пересоздаём контейнер через MakeSentient.
            RemComp<MindContainerComponent>(uid);
        }

        if (makeSentient)
            _mind.MakeSentient(uid, allowMovement, allowSpeech);

        // Уже есть роль (в т.ч. Taken) — снимаем и создаём заново, не трогая Taken/Allow* (Access).
        if (HasComp<GhostRoleComponent>(uid) || HasComp<GhostTakeoverAvailableComponent>(uid))
        {
            RemComp<GhostRoleComponent>(uid);
            RemComp<GhostTakeoverAvailableComponent>(uid);
        }

        var ghostRole = AddComp<GhostRoleComponent>(uid);
        EnsureComp<GhostTakeoverAvailableComponent>(uid);

        // RoleName / RoleDescription / RoleRules / RaffleConfig — Other ReadWriteExecute.
        ghostRole.RoleName = name;
        ghostRole.RoleDescription = description;
        ghostRole.RoleRules = rules ?? Loc.GetString("ghost-role-component-default-rules");
        ghostRole.RaffleConfig = raffleConfig;

        // ComponentStartup регистрирует роль; повтор через public API безопасен.
        _ghostRoles.RegisterGhostRole((uid, ghostRole));
        return true;
    }
}
