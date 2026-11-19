using System.Linq;
using Content.Server._Fish.Rpu;
using Content.Shared.AlertLevel;
using Content.Server.GameTicking;
using Content.Server.Objectives.Components;
using Content.Server.Station.Systems;
using Content.Shared._Fish.Rpu;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Rpu;

/// <summary>
/// Собирает множители штрафа и определяет случаи полного освобождения.
/// Всё считается только в момент нарушения, никого не опрашиваем по таймеру.
/// </summary>
public sealed partial class RpuModifierSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RpuSystem _rpu = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private AlertLevelSystem _alert = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private RpuAggressionSystem _aggression = default!;
    [Dependency] private GameTicker _ticker = default!;

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

    private readonly List<RpuModifierPrototype> _jobModifiers = new();
    private readonly List<RpuModifierPrototype> _departmentModifiers = new();
    private readonly List<RpuModifierPrototype> _antagModifiers = new();
    private readonly List<RpuModifierPrototype> _situationModifiers = new();
    private readonly List<RpuModifierPrototype> _alertModifiers = new();

    private readonly HashSet<ProtoId<AntagPrototype>> _activeRoundAntags = new();
    private TimeSpan _nextSituationRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnReload);
        ReloadModifiers();
    }

    private void OnReload(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<RpuModifierPrototype>())
            ReloadModifiers();
    }

    private void ReloadModifiers()
    {
        _jobModifiers.Clear();
        _departmentModifiers.Clear();
        _antagModifiers.Clear();
        _situationModifiers.Clear();
        _alertModifiers.Clear();

        foreach (var modifier in _prototypes.EnumeratePrototypes<RpuModifierPrototype>())
        {
            switch (modifier.Source)
            {
                case RpuModifierSource.Job: _jobModifiers.Add(modifier); break;
                case RpuModifierSource.Department: _departmentModifiers.Add(modifier); break;
                case RpuModifierSource.Antag: _antagModifiers.Add(modifier); break;
                case RpuModifierSource.StationSituation: _situationModifiers.Add(modifier); break;
                case RpuModifierSource.AlertLevel: _alertModifiers.Add(modifier); break;
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

    /// <summary>Собирает роли, отделы и цели игрока.</summary>
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

    /// <summary>
    /// Полный список множителей для нарушения. Перемножать их нельзя: итог считает
    /// RpuMaths.CombineMultipliers (взвешенное геометрическое среднее с затуханием весов).
    /// </summary>
    public List<float> CollectModifiers(ICommonSession session,
        RpuSuspect suspect,
        RpuCategory category,
        EntityUid? victim)
    {
        var modifiers = new List<float>();
        TryGetRoles(session, out var roles);

        foreach (var modifier in _jobModifiers)
        {
            if (roles.Job == null || !modifier.Jobs.Contains(roles.Job.Value))
                continue;

            if (modifier.TryGetMultiplier(category, out var value))
                modifiers.Add(value);
        }

        foreach (var modifier in _departmentModifiers)
        {
            if (!modifier.Departments.Overlaps(roles.Departments))
                continue;

            if (modifier.TryGetMultiplier(category, out var value))
                modifiers.Add(value);
        }

        foreach (var modifier in _antagModifiers)
        {
            if (!modifier.Antags.Overlaps(roles.Antags))
                continue;

            if (modifier.TryGetMultiplier(category, out var value))
                modifiers.Add(value);
        }

        // Обстановка на станции касается только самих антагонистов.
        if (roles.IsAntagonist)
        {
            foreach (var modifier in _situationModifiers)
            {
                if (modifier.Antags.Overlaps(_activeRoundAntags) &&
                    modifier.TryGetMultiplier(category, out var value))
                    modifiers.Add(value);
            }

            // Один антагонист на чистой станции выглядит подозрительнее.
            if (_activeRoundAntags.Count == 0)
                modifiers.Add(_rpu.Tuning.LonelyAntagMultiplier);
        }

        var alert = CurrentAlertLevel();
        foreach (var modifier in _alertModifiers)
        {
            if (alert == null || !modifier.AlertLevels.Contains(alert))
                continue;

            if (modifier.TryGetMultiplier(category, out var value))
                modifiers.Add(value);
        }

        if (category is RpuCategory.Kill or RpuCategory.KillSecurityChain)
            modifiers.Add(RpuMaths.KillEscalation(suspect.KillsThisRound));

        AppendPlaytimeModifiers(modifiers, suspect);
        modifiers.Add(suspect.BanHistoryMultiplier);

        return modifiers;
    }

    /// <summary>
    /// Чем меньше наигранного времени, тем сильнее давление: до 10 часов — ×1.5,
    /// до границы новичка — ×1.2, дальше 1.0.
    /// </summary>
    private void AppendPlaytimeModifiers(List<float> modifiers, RpuSuspect suspect)
    {
        var tuning = _rpu.Tuning;

        if (suspect.RoleHours < tuning.NewbieHours)
        {
            modifiers.Add(tuning.VeryNewMultiplier);
            return;
        }

        if (suspect.RoleHours < _rpu.NewbieBoundaryHours)
            modifiers.Add(tuning.NewbieMultiplier);
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

    /// <summary>
    /// Освобождает ли система нарушение полностью (множитель 0, без смешивания с остальными).
    /// </summary>
    public bool IsFullyExempt(ICommonSession session,
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
}


