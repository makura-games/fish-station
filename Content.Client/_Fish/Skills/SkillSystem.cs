using Content.Shared._Fish.Skills;
using Robust.Client.Player;

namespace Content.Client._Fish.Skills;

public sealed partial class SkillSystem : SharedSkillSystem
{
    [Dependency] private IPlayerManager _players = default!;

    public void RequestOpenSkillsUi()
    {
        if (_players.LocalEntity == null)
            return;

        RaiseNetworkEvent(new RequestOpenSkillsEuiEvent());
    }
}
