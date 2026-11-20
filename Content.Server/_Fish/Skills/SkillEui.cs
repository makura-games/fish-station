using Content.Server.EUI;
using Content.Shared._Fish.Skills;
using Content.Shared.Eui;
using Robust.Shared.Player;

namespace Content.Server._Fish.Skills;

public sealed class SkillEui : BaseEui
{
    private EntityUid _target;
    private readonly SkillSystem _skills;

    public EntityUid Target => _target;

    public SkillEui(EntityUid target, SkillSystem skills)
    {
        _target = target;
        _skills = skills;
    }

    public void Refresh()
    {
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return _skills.BuildEuiState(_target);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        switch (msg)
        {
            case SkillEuiSetLevelMessage set:
                _skills.TryAdjustLevel(_target, Player, set.SkillId, set.Delta);
                StateDirty();
                break;
            case SkillEuiConfirmMessage:
                _skills.TryConfirm(_target, Player);
                StateDirty();
                break;
            case SkillEuiResetMessage:
                _skills.TryReset(_target, Player);
                StateDirty();
                break;
        }
    }

    public override void Closed()
    {
        base.Closed();
        _skills.OnEuiClosed(Player);
    }
}
