using Content.Server._Fish.Objectives; // Fish-edit: компонент разума, покинувшего раунд активацией имплантата
using Content.Server.Objectives.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Mind;

namespace Content.Server.Objectives.Systems;

/// <summary>
/// Handles progress for the survive objective condition.
/// </summary>
public sealed partial class SurviveConditionSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurviveConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, SurviveConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        // Fish-start: игрок, сам покинувший раунд активацией имплантата (тело удалено), считается выжившим
        if (HasComp<ImplantSurvivedMindComponent>(args.MindId))
        {
            args.Progress = 1f;
            return;
        }
        // Fish-end

        args.Progress = _mind.IsCharacterDeadIc(args.Mind) ? 0f : 1f;
    }
}
