using Content.Shared._Fish.Skills;
using Content.Shared.DoAfter;
using Content.Shared.Paper;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Skills;

public sealed partial class SkillSystem
{
    private static readonly ProtoId<TagPrototype> BookTag = "Book";

    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private MetaDataSystem _meta = default!;

    private void InitializeAuthoring()
    {
        SubscribeLocalEvent<PaperComponent, GetVerbsEvent<AlternativeVerb>>(OnBookAuthorVerbs);
        SubscribeLocalEvent<PaperComponent, SkillManualAuthorDoAfterEvent>(OnAuthorFinished);
    }

    private void OnBookAuthorVerbs(EntityUid uid, PaperComponent paper, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!_tags.HasTag(uid, BookTag) || HasComp<SkillTrainingManualComponent>(uid))
            return;

        if (!TryComp<SkillsetComponent>(args.User, out var skillset) || !skillset.Locked)
            return;

        foreach (var skill in Prototypes.EnumeratePrototypes<SkillPrototype>())
        {
            var level = GetSkillValue(args.User, skill.ID, skillset);
            if (level < SkillLevels.Experienced)
                continue;

            var targetLevel = Math.Min(level, SkillMath.BookTrainingMaxLevel);
            var skillId = skill.ID;
            var skillName = Loc.GetString(skill.Name);
            var book = uid;
            var user = args.User;
            var target = targetLevel;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("skills-manual-verb-author",
                    ("skill", skillName),
                    ("level", Loc.GetString(SkillLevels.GetLevelNameKey(target)))),
                Act = () => TryStartAuthoring(user, book, skillId, target),
                Priority = 1,
            });
        }
    }

    private void TryStartAuthoring(EntityUid user, EntityUid book, ProtoId<SkillPrototype> skill, int targetLevel)
    {
        var delay = TimeSpan.FromSeconds(40 + 20 * Math.Max(0, targetLevel - SkillLevels.Basic));
        var ev = new SkillManualAuthorDoAfterEvent(skill, targetLevel);
        var args = new DoAfterArgs(EntityManager, user, delay, ev, book, used: book)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            CancelDuplicate = true,
        };
        _doAfter.TryStartDoAfter(args);
    }

    private void OnAuthorFinished(EntityUid book, PaperComponent paper, ref SkillManualAuthorDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (!TryComp<SkillsetComponent>(args.User, out var skillset) || !skillset.Locked)
            return;

        if (!_tags.HasTag(book, BookTag) || HasComp<SkillTrainingManualComponent>(book))
            return;

        var level = GetSkillValue(args.User, args.Skill, skillset);
        var target = Math.Min(Math.Min(level, args.TargetLevel), SkillMath.BookTrainingMaxLevel);
        if (level < SkillLevels.Experienced || target <= SkillLevels.Unskilled)
            return;

        var comp = EnsureComp<SkillTrainingManualComponent>(book);
        comp.Skill = args.Skill;
        comp.TargetLevel = target;
        comp.MinSkillToStudy = Math.Max(SkillLevels.Unskilled, target - 1);
        comp.StudyTime = TimeSpan.FromSeconds(35 + 25 * Math.Max(0, target - SkillLevels.Basic));
        Dirty(book, comp);

        var skillName = Prototypes.TryIndex(args.Skill, out var proto)
            ? Loc.GetString(proto.Name)
            : args.Skill.ToString();
        var levelName = Loc.GetString(SkillLevels.GetLevelNameKey(target));
        _meta.SetEntityName(book, Loc.GetString("skills-manual-name", ("skill", skillName), ("level", levelName)));
        _meta.SetEntityDescription(book, Loc.GetString("skills-manual-desc", ("skill", skillName), ("level", levelName)));

        _serverPopup.PopupEntity(Loc.GetString("skills-manual-authored"), args.User, args.User);
    }
}
