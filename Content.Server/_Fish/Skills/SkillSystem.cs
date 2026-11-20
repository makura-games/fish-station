using System.Linq;
using Content.Server.EUI;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Popups;
using Content.Shared._Fish.Skills;
using Content.Shared.Eui;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Skills;

/// <summary>
/// Серверная логика навыков: бюджет роли, распределение, обучение, эффекты.
/// </summary>
public sealed partial class SkillSystem : SharedSkillSystem
{
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private PopupSystem _serverPopup = default!;

    private static readonly ProtoId<SkillPrototype> ConstructionSkill = "FishSkillConstruction";
    private static readonly ProtoId<SkillPrototype> MedicalSkill = "FishSkillMedical";

    private readonly Dictionary<ICommonSession, SkillEui> _openUis = new();

    public ProtoId<SkillPrototype> Construction => ConstructionSkill;
    public ProtoId<SkillPrototype> Medical => MedicalSkill;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        // Fish-start: ghost role с JobProto (ОБР/ERT) — бюджет на takeover.
        // Не подписываемся на GhostTakeoverAvailableComponent+MindAddedMessage: слот уже занят GhostRoleSystem.
        SubscribeLocalEvent<GhostRoleComponent, MindAddedMessage>(OnGhostRoleMindAdded);
        // Fish-end
        SubscribeLocalEvent<SkillsetComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeNetworkEvent<RequestOpenSkillsEuiEvent>(OnRequestOpenSkills);
        InitializeAuthoring();
    }

    private void OnGhostRoleMindAdded(EntityUid uid, GhostRoleComponent ghostRole, MindAddedMessage args)
    {
        // NukeOps и прочие antag без JobProto остаются на Baseline (без Skillset).
        if (ghostRole.JobProto is not { } jobId)
            return;

        EnsureSkillset(uid, jobId);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId is not { } jobId)
            return;

        EnsureSkillset(args.Mob, jobId);

        // Автооткрытие только если есть свободные очки и распределение ещё не зафиксировано.
        if (TryComp<SkillsetComponent>(args.Mob, out var skillset) &&
            !skillset.Locked &&
            skillset.RemainingPoints > 0 &&
            TryComp<ActorComponent>(args.Mob, out var actor))
        {
            OpenSkillsUi(args.Mob, actor.PlayerSession);
        }
    }

    private void OnRequestOpenSkills(RequestOpenSkillsEuiEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid)
            return;

        if (!HasComp<SkillsetComponent>(uid))
            return;

        OpenSkillsUi(uid, args.SenderSession);
    }

    public void EnsureSkillset(EntityUid mob, ProtoId<JobPrototype> jobId)
    {
        var skillset = EnsureComp<SkillsetComponent>(mob);

        if (skillset.BudgetGranted && skillset.JobId != null && skillset.JobId != jobId)
        {
            skillset.Skills.Clear();
            skillset.CompletedTrainings.Clear();
            skillset.BudgetGranted = false;
            skillset.Locked = false;
        }

        if (skillset.BudgetGranted)
        {
            Dirty(mob, skillset);
            return;
        }

        skillset.JobId = jobId;
        skillset.TotalPoints = GetJobSkillPoints(jobId);
        skillset.RemainingPoints = skillset.TotalPoints;
        skillset.Skills.Clear();
        skillset.CompletedTrainings.Clear();
        skillset.Locked = false;

        foreach (var skill in Prototypes.EnumeratePrototypes<SkillPrototype>())
            skillset.Skills[skill.ID] = GetMinSkill(jobId, skill.ID);

        skillset.BudgetGranted = true;

        // Fish-start: бюджет без свободных очков (ОБР/ЦК и т.п.) сразу фиксируем — без окна на спавне
        if (skillset.RemainingPoints <= 0)
            skillset.Locked = true;
        // Fish-end

        Dirty(mob, skillset);

        if (TryComp<ActorComponent>(mob, out var actor) && skillset.RemainingPoints > 0)
        {
            _serverPopup.PopupEntity(
                Loc.GetString("skills-popup-budget-granted", ("points", skillset.RemainingPoints)),
                mob,
                actor.PlayerSession);
        }
    }

    private void OnGetVerbs(Entity<SkillsetComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || args.User != ent.Owner)
            return;

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        var session = actor.PlayerSession;
        var uid = ent.Owner;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("skills-verb-open"),
            Act = () => OpenSkillsUi(uid, session),
            Priority = 5,
        });
    }

    public void OpenSkillsUi(EntityUid uid, ICommonSession session)
    {
        if (!HasComp<SkillsetComponent>(uid))
            return;

        // Уже открыто на той же сущности — только обновить состояние.
        // При смене сущности закрываем старый EUI: иначе Confirm/Reset из старого окна
        // могут прийти после Retarget и залочить новую сущность.
        if (_openUis.TryGetValue(session, out var existing))
        {
            if (existing.Target == uid)
            {
                existing.Refresh();
                return;
            }

            _eui.CloseEui(existing);
            _openUis.Remove(session);
        }

        var eui = new SkillEui(uid, this);
        _openUis[session] = eui;
        _eui.OpenEui(eui, session);
    }

    public void OnEuiClosed(ICommonSession session)
    {
        _openUis.Remove(session);
    }

    public SkillEuiState BuildEuiState(EntityUid uid)
    {
        if (!TryComp<SkillsetComponent>(uid, out var skillset) || skillset.JobId is not { } jobId)
        {
            return new SkillEuiState(0, 0, true, null, Loc.GetString("skills-error-no-skillset"), new List<SkillUiEntry>());
        }

        string? jobName = null;
        if (Prototypes.TryIndex(jobId, out JobPrototype? job))
            jobName = job.LocalizedName;

        var entries = new List<SkillUiEntry>();
        foreach (var skill in Prototypes.EnumeratePrototypes<SkillPrototype>()
                     .OrderBy(s => s.Category)
                     .ThenBy(s => Loc.GetString(s.Name)))
        {
            var min = GetMinSkill(jobId, skill.ID);
            var max = GetMaxSkill(jobId, skill.ID);
            var current = GetSkillValue(uid, skill.ID, skillset);
            var nextCost = current < max ? skill.GetCostForLevel(current + 1) : 0;
            var prereqOk = CheckPrerequisites(uid, skill, skillset);
            string? block = prereqOk ? null : Loc.GetString("skills-error-prerequisites");

            var canIncrease = !skillset.Locked
                              && prereqOk
                              && current < max
                              && nextCost > 0
                              && skillset.RemainingPoints >= nextCost;

            entries.Add(new SkillUiEntry(
                skill.ID,
                Loc.GetString(skill.Name),
                Loc.GetString(skill.Description),
                skill.Category,
                current,
                min,
                max,
                nextCost,
                canIncrease,
                !skillset.Locked && current > min,
                block,
                FormatEffectHint(skill.ID, current)));
        }

        string? status = null;
        if (skillset.Locked)
            status = Loc.GetString("skills-status-confirmed");

        return new SkillEuiState(
            skillset.RemainingPoints,
            skillset.TotalPoints,
            skillset.Locked,
            jobName,
            status,
            entries);
    }

    /// <summary>
    /// Сдвиг уровня на delta (+1/−1) от актуального серверного значения.
    /// </summary>
    public void TryAdjustLevel(EntityUid uid, ICommonSession session, string skillId, int delta)
    {
        if (delta == 0)
            return;

        if (!TryComp<ActorComponent>(uid, out var actor) || actor.PlayerSession != session)
            return;

        if (!TryComp<SkillsetComponent>(uid, out var skillset) || skillset.JobId is not { } jobId)
            return;

        if (skillset.Locked || !skillset.BudgetGranted)
            return;

        if (!Prototypes.TryIndex<SkillPrototype>(skillId, out var skillProto))
            return;

        var min = GetMinSkill(jobId, skillProto.ID);
        var max = GetMaxSkill(jobId, skillProto.ID);
        var current = GetSkillValue(uid, skillProto.ID, skillset);
        var target = Math.Clamp(current + delta, min, max);
        if (target == current)
            return;

        if (!CheckPrerequisites(uid, skillProto, skillset) && target > min)
            return;

        var currentCost = skillProto.GetLevelCost(min, current);
        var newCost = skillProto.GetLevelCost(min, target);
        var pointDelta = newCost - currentCost;

        if (skillset.RemainingPoints - pointDelta < 0)
            return;

        skillset.RemainingPoints -= pointDelta;
        skillset.Skills[skillProto.ID] = target;
        PurgeInvalidPrerequisites((uid, skillset), jobId);
        Dirty(uid, skillset);
    }

    public void TryConfirm(EntityUid uid, ICommonSession session)
    {
        if (!TryComp<ActorComponent>(uid, out var actor) || actor.PlayerSession != session)
            return;

        if (!TryComp<SkillsetComponent>(uid, out var skillset) || !skillset.BudgetGranted)
            return;

        skillset.Locked = true;
        Dirty(uid, skillset);
        _serverPopup.PopupEntity(Loc.GetString("skills-popup-confirmed"), uid, session);
    }

    public void TryReset(EntityUid uid, ICommonSession session)
    {
        if (!TryComp<ActorComponent>(uid, out var actor) || actor.PlayerSession != session)
            return;

        if (!TryComp<SkillsetComponent>(uid, out var skillset) || skillset.Locked || skillset.JobId is not { } jobId)
            return;

        skillset.RemainingPoints = skillset.TotalPoints;
        foreach (var skill in Prototypes.EnumeratePrototypes<SkillPrototype>())
            skillset.Skills[skill.ID] = GetMinSkill(jobId, skill.ID);

        // Сброс до Confirm должен снова разрешать изучение тех же учебников.
        skillset.CompletedTrainings.Clear();

        Dirty(uid, skillset);
    }

    private void PurgeInvalidPrerequisites(Entity<SkillsetComponent> ent, ProtoId<JobPrototype> jobId)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var skill in Prototypes.EnumeratePrototypes<SkillPrototype>())
            {
                if (!ent.Comp.Skills.TryGetValue(skill.ID, out var level))
                    continue;

                var min = GetMinSkill(jobId, skill.ID);
                if (level <= min)
                    continue;

                if (CheckPrerequisites(ent, skill, ent.Comp))
                    continue;

                var refund = skill.GetLevelCost(min, level);
                ent.Comp.Skills[skill.ID] = min;
                ent.Comp.RemainingPoints += refund;
                changed = true;
            }
        }
    }
}
