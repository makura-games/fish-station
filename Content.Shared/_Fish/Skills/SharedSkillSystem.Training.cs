using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Skills;

public abstract partial class SharedSkillSystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    protected void InitializeTraining()
    {
        SubscribeLocalEvent<SkillTrainingManualComponent, UseInHandEvent>(OnManualUseInHand);
        SubscribeLocalEvent<SkillTrainingManualComponent, SkillManualStudyDoAfterEvent>(OnManualStudyFinished);
        SubscribeLocalEvent<SkillTrainingManualComponent, GetVerbsEvent<AlternativeVerb>>(OnManualGetVerbs);
    }

    private void OnManualGetVerbs(Entity<SkillTrainingManualComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!HasComp<SkillsetComponent>(args.User))
            return;

        var user = args.User;
        var manual = ent;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("skills-manual-verb-study"),
            Act = () => TryStartManualStudy(user, manual),
            Priority = 2,
        });
    }

    private void OnManualUseInHand(Entity<SkillTrainingManualComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryStartManualStudy(args.User, ent))
            args.Handled = true;
    }

    public bool TryStartManualStudy(EntityUid user, Entity<SkillTrainingManualComponent> manual)
    {
        if (!TryComp<SkillsetComponent>(user, out var skillset))
        {
            _popup.PopupEntity(Loc.GetString("skills-error-no-skillset"), user, user);
            return false;
        }

        if (!ValidateManualStudy(user, manual, skillset, out var failLoc))
        {
            if (failLoc != null)
                _popup.PopupEntity(Loc.GetString(failLoc), user, user);
            return false;
        }

        var args = new DoAfterArgs(EntityManager, user, manual.Comp.StudyTime, new SkillManualStudyDoAfterEvent(), manual, used: manual)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        return _doAfter.TryStartDoAfter(args);
    }

    private void OnManualStudyFinished(Entity<SkillTrainingManualComponent> ent, ref SkillManualStudyDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (!TryComp<SkillsetComponent>(args.User, out var skillset))
            return;

        if (!ValidateManualStudy(args.User, ent, skillset, out var failLoc))
        {
            if (failLoc != null)
                _popup.PopupEntity(Loc.GetString(failLoc), args.User, args.User);
            return;
        }

        var skill = ent.Comp.Skill;
        var target = ent.Comp.TargetLevel;
        var key = SkillMath.MakeTrainingKey(skill, target);

        skillset.Skills[skill] = target;
        skillset.CompletedTrainings.Add(key);
        Dirty(args.User, skillset);

        var skillName = Prototypes.TryIndex(skill, out var proto)
            ? Loc.GetString(proto.Name)
            : skill.ToString();

        _popup.PopupEntity(
            Loc.GetString("skills-manual-studied",
                ("skill", skillName),
                ("level", Loc.GetString(SkillLevels.GetLevelNameKey(target)))),
            args.User,
            args.User);

        if (ent.Comp.ConsumeOnStudy)
            PredictedQueueDel(ent.Owner);
    }

    private bool ValidateManualStudy(
        EntityUid user,
        Entity<SkillTrainingManualComponent> manual,
        SkillsetComponent skillset,
        out string? failLoc)
    {
        failLoc = null;
        var skill = manual.Comp.Skill;

        // Учебники только после Confirm — иначе бесплатные уровни можно вернуть в очки через TrySetLevel/Reset.
        if (!skillset.Locked)
        {
            failLoc = "skills-error-not-confirmed";
            return false;
        }

        if (manual.Comp.TargetLevel > SkillMath.BookTrainingMaxLevel)
        {
            failLoc = "skills-manual-error-master-locked";
            return false;
        }

        var target = Math.Clamp(manual.Comp.TargetLevel, SkillLevels.Min, SkillMath.BookTrainingMaxLevel);

        if (!Prototypes.TryIndex(skill, out SkillPrototype? skillProto))
        {
            failLoc = "skills-error-unknown-skill";
            return false;
        }

        if (!CheckPrerequisites(user, skillProto, skillset))
        {
            failLoc = "skills-error-prerequisites";
            return false;
        }

        var current = GetSkillValue(user, skill, skillset);
        if (current >= target)
        {
            failLoc = "skills-manual-error-already-known";
            return false;
        }

        if (current < manual.Comp.MinSkillToStudy || current != target - 1)
        {
            failLoc = "skills-manual-error-need-previous";
            return false;
        }

        var key = SkillMath.MakeTrainingKey(skill, target);
        if (skillset.CompletedTrainings.Contains(key))
        {
            failLoc = "skills-manual-error-already-studied";
            return false;
        }

        if (skillset.JobId is { } jobId)
        {
            var max = GetMaxSkill(jobId, skill);
            if (target > max)
            {
                failLoc = "skills-manual-error-job-cap";
                return false;
            }
        }

        return true;
    }
}

[Serializable, NetSerializable]
public sealed partial class SkillManualStudyDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// DoAfter написания учебного пособия. Должен жить в Shared из‑за NetSerializable.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SkillManualAuthorDoAfterEvent : SimpleDoAfterEvent
{
    public ProtoId<SkillPrototype> Skill;
    public int TargetLevel;

    public SkillManualAuthorDoAfterEvent(ProtoId<SkillPrototype> skill, int targetLevel)
    {
        Skill = skill;
        TargetLevel = targetLevel;
    }

    public SkillManualAuthorDoAfterEvent()
    {
    }
}

