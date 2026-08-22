using System.Linq;
using System.Numerics;
using Content.Shared._Fish.Arcade.Minesweeper;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Fish.Arcade.Minesweeper;

public sealed class MinesweeperMenu : DefaultWindow
{

    private const int CellSize = 32;

    private static readonly SpriteSpecifier FlagIcon = new SpriteSpecifier.Rsi(
        new ResPath("/Textures/Objects/Misc/Handy_Flags/NT_handy_flag.rsi"), "icon");

    private static readonly SpriteSpecifier MineIcon = new SpriteSpecifier.Rsi(
        new ResPath("/Textures/Structures/Machines/bomb.rsi"), "syndicate-bomb");

    private static readonly Color HiddenColor = Color.FromHex("#464966");
    private static readonly Color RevealedColor = Color.FromHex("#26283A");
    private static readonly Color ExplodedColor = Color.FromHex("#8C2A2A");
    private static readonly Color WrongFlagColor = Color.FromHex("#6B3A48");
    private static readonly Color GridLineColor = Color.FromHex("#15161D");
    private static readonly Color[] AdjacentColors =
    {
        Color.FromHex("#4C8FFF"),
        Color.FromHex("#4CD964"),
        Color.FromHex("#FF5C5C"),
        Color.FromHex("#B98CFF"),
        Color.FromHex("#FFB74C"),
        Color.FromHex("#4CE0E0"),
        Color.FromHex("#FFFFFF"),
        Color.FromHex("#A0A0A0"),
    };

    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly Texture _flagTexture;
    private readonly Texture _mineTexture;

    private readonly Button _newGameButton;
    private readonly Label _minesLabel;
    private readonly Label _timerLabel;
    private readonly Label _statusLabel;
    private readonly Label _scoresTitle;
    private readonly BoxContainer _scoresBox;
    private readonly GridContainer _boardGrid;

    private readonly Dictionary<MinesweeperDifficulty, Button> _difficultyButtons = new();

    private MinesweeperCellButton[] _cells = Array.Empty<MinesweeperCellButton>();
    private MinesweeperUiState? _state;

    /// <summary>
    ///     Размер поля, под который сейчас собрана сетка кнопок.
    /// </summary>
    private int _boardWidth;

    private int _boardHeight;

    /// <summary>
    ///     Последнее показанное на таймере время в секундах. Нужно, чтобы не пересобирать строку каждый кадр.
    /// </summary>
    private int _shownSeconds = -1;

    /// <summary>
    ///     Игрок открывает клетку.
    /// </summary>
    public event Action<int, int>? OnReveal;

    /// <summary>
    ///     Игрок переключает флажок на клетке.
    /// </summary>
    public event Action<int, int>? OnFlag;

    /// <summary>
    ///     Игрок начинает новую партию на выбранной сложности.
    /// </summary>
    public event Action<MinesweeperDifficulty>? OnNewGame;

    public MinesweeperMenu()
    {
        IoCManager.InjectDependencies(this);

        var sprite = _entMan.System<SpriteSystem>();
        _flagTexture = sprite.Frame0(FlagIcon);
        _mineTexture = sprite.Frame0(MineIcon);

        // Размер не задаём: окно подстраивается под текущее поле, иначе на лёгком уровне
        // остаётся огромная пустая рамка.
        Title = Loc.GetString("minesweeper-menu-title");

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };

        var difficultyBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 4,
        };

        var difficultyGroup = new ButtonGroup();

        foreach (var difficulty in MinesweeperDifficultySettings.All)
        {
            var settings = MinesweeperDifficultySettings.Get(difficulty);
            var button = new Button
            {
                Text = Loc.GetString(GetDifficultyKey(difficulty)),
                ToggleMode = true,
                Group = difficultyGroup,
                HorizontalExpand = true,
                ToolTip = Loc.GetString("minesweeper-difficulty-tooltip",
                    ("width", settings.Width),
                    ("height", settings.Height),
                    ("mines", settings.Mines)),
            };

            button.OnPressed += _ => OnNewGame?.Invoke(difficulty);

            _difficultyButtons[difficulty] = button;
            difficultyBox.AddChild(button);
        }

        root.AddChild(difficultyBox);

        var infoBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
        };

        _minesLabel = new Label { HorizontalExpand = true };
        _timerLabel = new Label { HorizontalExpand = true, Align = Label.AlignMode.Right };
        _newGameButton = new Button { Text = Loc.GetString("minesweeper-button-new-game") };
        _newGameButton.OnPressed += _ =>
        {
            if (_state != null)
                OnNewGame?.Invoke(_state.Difficulty);
        };

        infoBox.AddChild(_minesLabel);
        infoBox.AddChild(_newGameButton);
        infoBox.AddChild(_timerLabel);
        root.AddChild(infoBox);

        _boardGrid = new GridContainer
        {
            Columns = 1,
            HSeparationOverride = 1,
            VSeparationOverride = 1,
        };

        var boardPanel = new PanelContainer
        {
            HorizontalAlignment = HAlignment.Center,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = GridLineColor,
                ContentMarginLeftOverride = 1,
                ContentMarginTopOverride = 1,
                ContentMarginRightOverride = 1,
                ContentMarginBottomOverride = 1,
            },
        };
        boardPanel.AddChild(_boardGrid);
        root.AddChild(boardPanel);

        _statusLabel = new Label { Align = Label.AlignMode.Center, HorizontalExpand = true };
        root.AddChild(_statusLabel);

        root.AddChild(new Label
        {
            Align = Label.AlignMode.Center,
            HorizontalExpand = true,
            Text = Loc.GetString("minesweeper-hint-controls"),
            FontColorOverride = Color.Gray,
        });

        _scoresTitle = new Label { Align = Label.AlignMode.Center, HorizontalExpand = true };
        root.AddChild(_scoresTitle);

        _scoresBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 2,
        };
        root.AddChild(_scoresBox);

        ContentsContainer.AddChild(root);

        // Собираем поле сразу: окно центрируется по своему размеру в момент открытия,
        // а состояние с сервера придёт уже после этого.
        var startup = MinesweeperDifficultySettings.Get(MinesweeperDifficulty.Easy);
        RebuildBoard(startup.Width, startup.Height);
    }

    /// <summary>
    ///     Принимает новый снимок состояния с сервера.
    /// </summary>
    public void Update(MinesweeperUiState state)
    {
        _state = state;

        if (_boardWidth != state.Width || _boardHeight != state.Height)
            RebuildBoard(state.Width, state.Height);

        var isPlayer = state.Player != null && _entMan.GetEntity(state.Player.Value) == _player.LocalEntity;

        for (var index = 0; index < _cells.Length && index < state.Board.Length; index++)
        {
            UpdateCell(_cells[index], state.Board[index]);
        }

        foreach (var (difficulty, button) in _difficultyButtons)
        {
            button.Pressed = difficulty == state.Difficulty;
            button.Disabled = !isPlayer;
        }

        _newGameButton.Disabled = !isPlayer;
        _minesLabel.Text = Loc.GetString("minesweeper-label-mines", ("count", state.MinesRemaining));
        _statusLabel.Text = Loc.GetString(GetStatusKey(state.Status, isPlayer));
        _statusLabel.FontColorOverride = GetStatusColor(state.Status);

        UpdateScores(state);
        UpdateTimer();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        UpdateTimer();
    }

    /// <summary>
    ///     Пересобирает сетку клеток под новый размер поля.
    /// </summary>
    private void RebuildBoard(int width, int height)
    {
        _boardGrid.RemoveAllChildren();
        _boardGrid.Columns = width;

        _boardWidth = width;
        _boardHeight = height;
        _cells = new MinesweeperCellButton[width * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var cellX = x;
                var cellY = y;

                var button = new MinesweeperCellButton(cellX, cellY, HiddenColor)
                {
                    MinSize = new Vector2(CellSize, CellSize),
                };

                button.OnPressed += _ => OnReveal?.Invoke(cellX, cellY);
                button.OnRightPressed += _ => OnFlag?.Invoke(cellX, cellY);

                _cells[cellY * width + cellX] = button;
                _boardGrid.AddChild(button);
            }
        }
    }

    private void UpdateCell(MinesweeperCellButton button, MinesweeperCell cell)
    {
        switch (cell)
        {
            case MinesweeperCell.Hidden:
                button.SetColor(HiddenColor);
                button.SetText(string.Empty, Color.White);
                break;
            case MinesweeperCell.Flagged:
                button.SetColor(HiddenColor);
                button.SetIcon(_flagTexture);
                break;
            case MinesweeperCell.FlagWrong:
                button.SetColor(WrongFlagColor);
                button.SetIcon(_flagTexture);
                break;
            case MinesweeperCell.Mine:
                button.SetColor(HiddenColor);
                button.SetIcon(_mineTexture);
                break;
            case MinesweeperCell.MineExploded:
                button.SetColor(ExplodedColor);
                button.SetIcon(_mineTexture);
                break;
            default:
                var adjacent = cell.GetAdjacent() ?? 0;
                var text = adjacent == 0 ? string.Empty : adjacent.ToString();
                var color = adjacent == 0 ? Color.White : AdjacentColors[adjacent - 1];

                button.SetColor(RevealedColor);
                button.SetText(text, color);
                break;
        }
    }

    private void UpdateScores(MinesweeperUiState state)
    {
        _scoresTitle.Text = Loc.GetString("minesweeper-scores-title",
            ("difficulty", Loc.GetString(GetDifficultyKey(state.Difficulty))));

        _scoresBox.RemoveAllChildren();

        var scores = state.Scores
            .Where(entry => entry.Difficulty == state.Difficulty)
            .OrderBy(entry => entry.Time)
            .ToList();

        if (scores.Count == 0)
        {
            _scoresBox.AddChild(new Label
            {
                Align = Label.AlignMode.Center,
                HorizontalExpand = true,
                Text = Loc.GetString("minesweeper-scores-empty"),
                FontColorOverride = Color.Gray,
            });
            return;
        }

        for (var i = 0; i < scores.Count; i++)
        {
            _scoresBox.AddChild(new Label
            {
                HorizontalExpand = true,
                Text = Loc.GetString("minesweeper-scores-entry",
                    ("place", i + 1),
                    ("name", scores[i].Name),
                    ("time", FormatTime(scores[i].Time))),
            });
        }
    }

    /// <summary>
    ///     Пересчитывает таймер по серверным отсечкам. Пока партия идёт, время тикает локально.
    /// </summary>
    private void UpdateTimer()
    {
        var elapsed = TimeSpan.Zero;

        if (_state?.StartTime is { } startTime)
            elapsed = (_state.EndTime ?? _timing.CurTime) - startTime;

        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        var seconds = (int) elapsed.TotalSeconds;
        if (seconds == _shownSeconds)
            return;

        _shownSeconds = seconds;
        _timerLabel.Text = Loc.GetString("minesweeper-label-time", ("time", FormatTime(elapsed)));
    }

    private static string FormatTime(TimeSpan time)
    {
        return $"{(int) time.TotalMinutes:00}:{time.Seconds:00}";
    }

    private static string GetDifficultyKey(MinesweeperDifficulty difficulty)
    {
        return difficulty switch
        {
            MinesweeperDifficulty.Normal => "minesweeper-difficulty-normal",
            MinesweeperDifficulty.Hard => "minesweeper-difficulty-hard",
            _ => "minesweeper-difficulty-easy",
        };
    }

    private static string GetStatusKey(MinesweeperStatus status, bool isPlayer)
    {
        if (!isPlayer)
            return "minesweeper-status-spectator";

        return status switch
        {
            MinesweeperStatus.Playing => "minesweeper-status-playing",
            MinesweeperStatus.Won => "minesweeper-status-won",
            MinesweeperStatus.Lost => "minesweeper-status-lost",
            _ => "minesweeper-status-ready",
        };
    }

    private static Color GetStatusColor(MinesweeperStatus status)
    {
        return status switch
        {
            MinesweeperStatus.Won => AdjacentColors[1],
            MinesweeperStatus.Lost => AdjacentColors[2],
            _ => Color.White,
        };
    }
}
