using System.Linq;
using Content.Client.UserInterface.Systems.Ghost.Controls.Roles;
using Content.Shared._Fish.Antag;
using Robust.Client.UserInterface;

namespace Content.Client._Fish.Antag;

public sealed class FishAntagRulesClientSystem : EntitySystem
{
    private GhostRoleRulesWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FishAntagRulesEvent>(OnAntagRulesReceived);
    }

    private void OnAntagRulesReceived(FishAntagRulesEvent ev)
    {
        var rules = string.Join("\n\n", ev.Rules.Select(Loc.GetString));
        _window?.Close();
        _window = new GhostRoleRulesWindow(rules);
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
    }
}
