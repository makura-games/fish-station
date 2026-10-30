using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mindshield;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Antag;

/// <summary>
/// Единая проверка защиты персонажа от выбора целью антагонистов.
/// </summary>
public sealed partial class SharedAntagTargetingProtectionSystem : EntitySystem
{
    public static readonly ProtoId<DepartmentPrototype> CommandDepartment = "Command";
    public static readonly ProtoId<DepartmentPrototype> SecurityDepartment = "Security";
    public static readonly ProtoId<DepartmentPrototype> CivilianDepartment = "Civilian";
    public static readonly ProtoId<DepartmentPrototype> ServiceDepartment = "Service";

    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private MindShieldSystem _mindShield = default!;
    [Dependency] private ISharedPlaytimeManager _playtime = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ISharedPlayerManager _player = default!;

    /// <summary>
    /// Проверяет, защищён ли разум от назначения целью антага.
    /// </summary>
    public bool IsProtected(EntityUid mindId, MindComponent? mind = null)
    {
        if (!Resolve(mindId, ref mind, false))
            return false;

        if (mind.OwnedEntity is not { } body)
            return false;

        return IsBodyProtected(body, mindId, mind);
    }

    /// <summary>
    /// Проверяет тело (и связанный разум, если есть).
    /// </summary>
    public bool IsBodyProtected(EntityUid body, EntityUid? mindId = null, MindComponent? mind = null)
    {
        // Черта из редактора персонажа.
        if (HasComp<AntagTargetingProtectedComponent>(body))
            return true;

        // МШ
        _mindShield.GetMindshieldStatus(body, out var shielded, out _);
        if (shielded)
            return true;

        // Роли / департаменты
        if (TryGetJob(body, mindId, mind, out var job) && job is { } protectedJob && IsProtectedJob(protectedJob))
            return true;

        // Новички по Overall playtime
        if (IsNewbie(body, mindId, mind))
            return true;

        return false;
    }

    public bool IsProtectedJob(ProtoId<JobPrototype> job)
    {
        return _jobs.JobIsInDepartment(job, CommandDepartment)
               || _jobs.JobIsInDepartment(job, SecurityDepartment)
               || _jobs.JobIsInDepartment(job, CivilianDepartment)
               || _jobs.JobIsInDepartment(job, ServiceDepartment);
    }

    private bool TryGetJob(EntityUid body, EntityUid? mindId, MindComponent? mind, out ProtoId<JobPrototype>? job)
    {
        job = null;

        if (mindId != null && _jobs.MindTryGetJobId(mindId, out var mindJob))
        {
            job = mindJob;
            return true;
        }

        if (TryComp<MindContainerComponent>(body, out var container) && container.Mind is { } mid
            && _jobs.MindTryGetJobId(mid, out var bodyJob))
        {
            job = bodyJob;
            return true;
        }

        return false;
    }

    private bool IsNewbie(EntityUid body, EntityUid? mindId, MindComponent? mind)
    {
        var hours = _cfg.GetCVar(FishCVars.AntagTargetNewbiePlaytimeHours);
        if (hours <= 0)
            return false;

        ICommonSession? session = null;

        if (mindId != null && Resolve(mindId.Value, ref mind, false) && mind.UserId is { } userId)
            _player.TryGetSessionById(userId, out session);

        if (session == null)
            _player.TryGetSessionByEntity(body, out session);

        if (session == null)
            return false;

        var playTimes = _playtime.GetPlayTimes(session);
        if (!playTimes.TryGetValue(PlayTimeTrackingShared.TrackerOverall, out var overall))
            return true; // нет данных — считаем новичком

        return overall < TimeSpan.FromHours(hours);
    }
}
