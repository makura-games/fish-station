using Content.Server.GameTicking;
using Content.Server.Objectives.Components;
using Content.Server.Station.Systems;
using Content.Shared._Fish.Rpu;
using Content.Shared.AlertLevel;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Собирает поправки в очках кармы и решает, вменяется ли нарушение игроку вовсе.
/// Всё считается только в момент нарушения, никого не опрашиваем по таймеру.
/// </summary>
public sealed partial class RpuAdjustSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RpuSystem _rpu = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private AlertLevelSystem _alert = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private RpuAggressionSystem _aggression = default!;

    /// <summary>Роли и цели подозреваемого на момент нарушения.</summary>
    public sealed class SuspectRoles
    {
        public ProtoId<JobPrototype>? Job;
        public readonly List<ProtoId<DepartmentPrototype>> Departments = new();
        public readonly HashSet<ProtoId<AntagPrototype>> Antags = new();
        public readonly HashSet<EntityUid> ObjectiveTargets = new();
        public string? MobPrototype;
        public bool IsAntagonist;
        public bool CanJackDoors;

        public bool HasAnyRole => Job != null || Antags.Count > 0;
    }

    private readonly List<RpuAdjustPrototype> _jobAdjusts = new();
    private readonly List<RpuAdjustPrototype> _departmentAdjusts = new();
    private readonly List<RpuAdjustPrototype> _antagAdjusts = new();
    private readonly List<RpuAdjustPrototype> _situationAdjusts = new();
    private readonly List<RpuAdjustPrototype> _alertAdjusts = new();

    private readonly HashSet<ProtoId<AntagPrototype>> _activeRoundAntags = new();
    private TimeSpan _nextSituationRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnReload);
        ReloadAdjusts();
    }

    private void OnReload(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<RpuAdjustPrototype>())
            ReloadAdjusts();
    }

    private void ReloadAdjusts()
    {
        _jobAdjusts.Clear();
        _departmentAdjusts.Clear();
        _antagAdjusts.Clear();
        _situationAdjusts.Clear();
        _alertAdjusts.Clear();

        foreach (var adjust in _prototypes.EnumeratePrototypes<RpuAdjustPrototype>())
        {
            switch (adjust.Source)
            {
                case RpuAdjustSource.Job: _jobAdjusts.Add(adjust); break;
                case RpuAdjustSource.Department: _departmentAdjusts.Add(adjust); break;
                case RpuAdjustSource.Antag: _antagAdjusts.Add(adjust); break;
                case RpuAdjustSource.StationSituation: _situationAdjusts.Add(adjust); break;
                case RpuAdjustSource.AlertLevel: _alertAdjusts.Add(adjust); break;
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Обновляем обстановку редко и только если раунд идёт.
        if (_timing.CurTime < _nextSituationRefresh || !_rpu.Active)
            return;

        _nextSituationRefresh = _timing.CurTime + TimeSpan.FromSeconds(30);
        RefreshActiveAntags();
    }

    private void RefreshActiveAntags()
    {
        _activeRoundAntags.Clear();

        var query = EntityQueryEnumerator<MindRoleComponent>();
        while (query.MoveNext(out _, out var role))
        {
            if (role.AntagPrototype is { } antag)
                _activeRoundAntags.Add(antag);
        }
    }

    /// <summary>
    /// Собирает поправки в очках: плюс — отягчающие, минус — смягчающие.
    /// Складываем их с базовой ценой нарушения в RpuSystem.Penalize.
    /// </summary>
    public List<float> CollectUnits(ICommonSession session,
        RpuSuspect suspect,
        RpuCategory category,
        EntityUid? victim)
    {
        var units = new List<float>();
        TryGetRoles(session, out var roles);

        foreach (var adjust in _jobAdjusts)
        {
            if (roles.Job == null || !adjust.Jobs.Contains(roles.Job.Value))
                continue;

            AddUnits(units, adjust, category, roles);
        }

        foreach (var adjust in _departmentAdjusts)
        {
            if (!adjust.Departments.Overlaps(roles.Departments))
                continue;

            AddUnits(units, adjust, category, roles);
        }

        foreach (var adjust in _antagAdjusts)
        {
            if (!adjust.Antags.Overlaps(roles.Antags))
                continue;

            AddUnits(units, adjust, category, roles);
        }

        // Обстановка на станции касается только самих антагонистов.
        if (roles.IsAntagonist)
        {
            foreach (var adjust in _situationAdjusts)
            {
                if (adjust.Antags.Overlaps(_activeRoundAntags))
                    AddUnits(units, adjust, category, roles);
            }

            // Один антагонист на чистой станции выглядит подозрительнее.
            if (_activeRoundAntags.Count == 0)
                units.Add(_rpu.Tuning.LonelyAntagUnits);
        }

        var alert = CurrentAlertLevel();
        if (alert != null)
        {
            foreach (var adjust in _alertAdjusts)
            {
                if (adjust.AlertLevels.Contains(alert))
                    AddUnits(units, adjust, category, roles);
            }
        }

        if (category is RpuCategory.Kill or RpuCategory.KillSecurityChain)
            units.Add(RpuMaths.KillUnits(suspect.KillsThisRound));

        units.Add(PlaytimeUnits(suspect));
        units.Add(suspect.BanHistoryUnits);

        return units;
    }

    private static void AddUnits(List<float> units, RpuAdjustPrototype adjust, RpuCategory category, SuspectRoles roles)
    {
        if (adjust.OnlyForAntagonists && !roles.IsAntagonist)
            return;

        if (adjust.TryGetUnits(category, out var value))
            units.Add(value);
    }

    /// <summary>
    /// Очки сверху за малое наигранное время: до VeryNewHours — максимум,
    /// до NewbieHours — меньше, дальше ничего.
    /// </summary>
    private float PlaytimeUnits(RpuSuspect suspect)
    {
        var tuning = _rpu.Tuning;

        if (suspect.RoleHours < tuning.VeryNewHours)
            return tuning.VeryNewUnits;

        if (suspect.RoleHours < tuning.NewbieHours)
            return tuning.NewbieUnits;

        return 0f;
    }

    private string? CurrentAlertLevel()
    {
        foreach (var station in _station.GetStations())
        {
            if (_alert.TryGetLevel(station, out var level))
                return level;
        }

        return null;
    }

    /// <summary>
    /// Вменяется ли вообще игроку нарушение этого вида. Здесь же — прежние случаи
    /// полного освобождения (цель по заданию, иммунная роль, иммунный моб).
    /// </summary>
    public bool IsWaived(ICommonSession session,
        RpuSuspect suspect,
        RpuCategory category,
        EntityUid? victim)
    {
        var tuning = _rpu.Tuning;
        TryGetRoles(session, out var roles);

        // Животные и фауна (кроме Pun Pun) системой не считаются.
        if (roles.MobPrototype != null && tuning.ImmuneMobs.Contains(roles.MobPrototype))
            return true;

        if (roles.Job != null && tuning.ImmuneJobs.Contains(roles.Job.Value))
            return true;

        if (roles.Antags.Overlaps(tuning.ImmuneAntags))
            return true;

        // Игрок без должности и без антагонисткой роли — гостевое животное либо наблюдатель.
        if (!roles.HasAnyRole)
            return true;

        // Профессия, для которой это работа: «не отвечаю за эту категорию».
        foreach (var adjust in _jobAdjusts)
        {
            if (roles.Job != null && adjust.Jobs.Contains(roles.Job.Value) && adjust.WaivesCategory(category))
                return true;
        }

        foreach (var adjust in _departmentAdjusts)
        {
            if (adjust.Departments.Overlaps(roles.Departments) && adjust.WaivesCategory(category))
                return true;
        }

        foreach (var adjust in _antagAdjusts)
        {
            if (adjust.Antags.Overlaps(roles.Antags) && adjust.WaivesCategory(category))
                return true;
        }

        if (victim == null)
            return false;

        TryGetRolesByEntity(victim.Value, out var victimRoles);

        // Жертва из «разрешённого» списка: крупные анты, зомби, ядерные оперативники.
        if (victimRoles.Antags.Overlaps(tuning.FreeKillAntags))
            return true;

        // Жертва — цель игрока.
        if (roles.ObjectiveTargets.Contains(victim.Value))
            return true;

        if (category == RpuCategory.AirlockHack && roles.CanJackDoors)
            return true;

        // Мелкий антагонист напал первым — защищаться можно.
        if (victimRoles.Antags.Overlaps(tuning.FirstStrikeAntags)
            && _aggression.AttackedRecently(victim, session.AttachedEntity))
            return true;

        return false;
    }

    /// <summary>Собирает роли по сессии — нужно вызывающим системам.</summary>
    public bool TryGetRoles(ICommonSession? session, out SuspectRoles roles)
    {
        roles = new SuspectRoles();

        if (session?.AttachedEntity is { } mob)
        {
            var meta = MetaData(mob);
            roles.MobPrototype = meta.EntityPrototype?.ID;
        }

        if (session == null || !_mind.TryGetMind(session, out var mindId, out var mind))
            return false;

        FillRoles(mindId, mind, roles);
        return true;
    }

    /// <summary>Разбирает роли по сущности (жертвы, нарушители из событий).</summary>
    public bool TryGetRolesByEntity(EntityUid uid, out SuspectRoles roles)
    {
        roles = new SuspectRoles();
        roles.MobPrototype = MetaData(uid).EntityPrototype?.ID;

        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return roles.MobPrototype != null;

        FillRoles(mindId, mind, roles);
        return true;
    }

    private void FillRoles(EntityUid mindId, MindComponent mind, SuspectRoles roles)
    {
        if (_jobs.MindTryGetJobId(mindId, out var job) && job is { } jobId)
        {
            roles.Job = jobId;

            if (_jobs.TryGetAllDepartments(jobId, out var departments))
            {
                foreach (var department in departments)
                    roles.Departments.Add(new ProtoId<DepartmentPrototype>(department.ID));
            }
        }

        foreach (var role in mind.MindRoleContainer.ContainedEntities)
        {
            if (TryComp<MindRoleComponent>(role, out var mindRole) && mindRole.AntagPrototype is { } antag)
            {
                roles.Antags.Add(antag);
                roles.IsAntagonist = true;
            }

            if (TryComp<TargetObjectiveComponent>(role, out var target) && target.Target is { } targetUid)
                roles.ObjectiveTargets.Add(targetUid);

            if (HasComp<DoorjackConditionComponent>(role))
                roles.CanJackDoors = true;
        }
    }
}
