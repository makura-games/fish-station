using Content.Client.Eui;
using Content.Shared._Fish.Skills;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Fish.Skills;

[UsedImplicitly]
public sealed class SkillEui : BaseEui
{
    private readonly SkillWindow _window;

    public SkillEui()
    {
        _window = new SkillWindow();
        _window.OnIncrease += id => SendMessage(new SkillEuiSetLevelMessage(id, _window.GetLevel(id) + 1));
        _window.OnDecrease += id => SendMessage(new SkillEuiSetLevelMessage(id, _window.GetLevel(id) - 1));
        _window.OnConfirm += () => SendMessage(new SkillEuiConfirmMessage());
        _window.OnReset += () => SendMessage(new SkillEuiResetMessage());
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is SkillEuiState skillState)
            _window.UpdateState(skillState);
    }
}
