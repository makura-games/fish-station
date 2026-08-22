using Content.Shared._Fish.Arcade.Minesweeper;
using Robust.Client.UserInterface;

namespace Content.Client._Fish.Arcade.Minesweeper;

public sealed class MinesweeperBoundUserInterface : BoundUserInterface
{
    private MinesweeperMenu? _menu;

    public MinesweeperBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<MinesweeperMenu>();
        _menu.OnReveal += (x, y) => SendMessage(new MinesweeperRevealMessage(x, y));
        _menu.OnFlag += (x, y) => SendMessage(new MinesweeperFlagMessage(x, y));
        _menu.OnNewGame += difficulty => SendMessage(new MinesweeperNewGameMessage(difficulty));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is MinesweeperUiState minesweeperState)
            _menu?.Update(minesweeperState);
    }
}
