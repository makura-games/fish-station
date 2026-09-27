using Content.Client.Administration.Managers;
using Content.Client.Gameplay;
using Content.Shared._Fish.PerformanceGuardian;
using Content.Shared.Administration;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Timing;

namespace Content.Client._Fish.PerformanceGuardian;

[UsedImplicitly]
public sealed partial class PerformanceGuardianUIController : UIController,
    IOnStateEntered<GameplayState>,
    IOnStateExited<GameplayState>,
    IOnSystemChanged<PerformanceGuardianSystem>
{
    [Dependency] private IClientAdminManager _admin = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConsoleHost _con = default!;

    private PerformanceGuardianWindow? _window;
    private PerformanceGuardianSystem? _system;
    private TimeSpan _nextRequest;
    private float _refreshSeconds = 2f;
    private bool _subscribed;

    public override void Initialize()
    {
        base.Initialize();
        _con.RegisterCommand("perfguardian", Loc.GetString("pg-cmd-desc"), Loc.GetString("pg-cmd-help"), OnCommand);
        _con.RegisterCommand("pg", Loc.GetString("pg-cmd-desc"), Loc.GetString("pg-cmd-help"), OnCommand);
        _cfg.OnValueChanged(FishCCVars.PgUiRefreshSeconds, v => _refreshSeconds = Math.Max(1f, v), true);
    }

    private void OnCommand(IConsoleShell shell, string argStr, string[] args) => ToggleWindow();

    public void OnStateEntered(GameplayState state)
    {
    }

    public void OnStateExited(GameplayState state) => CloseWindow();

    public void OnSystemLoaded(PerformanceGuardianSystem system)
    {
        _system = system;
        system.ReportReceived += OnReport;
        system.OpenWindowRequested += OpenWindow;
    }

    public void OnSystemUnloaded(PerformanceGuardianSystem system)
    {
        system.ReportReceived -= OnReport;
        system.OpenWindowRequested -= OpenWindow;
        CloseWindow();
        _system = null;
    }

    public void ToggleWindow()
    {
        if (_window?.IsOpen == true)
            CloseWindow();
        else
            OpenWindow();
    }

    public void OpenWindow()
    {
        if (!_admin.HasFlag(AdminFlags.Debug))
            return;

        EnsureWindow();
        _window!.OpenCentered();
        Subscribe();
        _system?.RequestReport();
    }

    public void CloseWindow()
    {
        Unsubscribe();
        if (_window == null)
            return;

        _window.RefreshPressed -= OnRefresh;
        _window.DiagnosePressed -= OnDiagnose;
        _window.Close();
        _window = null;
    }

    private void EnsureWindow()
    {
        if (_window != null)
            return;

        _window = UIManager.CreateWindow<PerformanceGuardianWindow>();
        _window.OnClose += () =>
        {
            Unsubscribe();
            _window = null;
        };
        _window.RefreshPressed += OnRefresh;
        _window.DiagnosePressed += OnDiagnose;
    }

    private void OnRefresh() => _system?.RequestReport();
    private void OnDiagnose() => _system?.RequestDiagnose();

    private void OnReport(PgReport report)
    {
        if (_window is not { IsOpen: true })
            return;

        _window.Apply(report);
    }

    private void Subscribe()
    {
        if (_subscribed || _system == null)
            return;

        _system.Subscribe();
        _subscribed = true;
        _nextRequest = _timing.RealTime + TimeSpan.FromSeconds(_refreshSeconds);
    }

    private void Unsubscribe()
    {
        if (!_subscribed || _system == null)
            return;

        _system.Unsubscribe();
        _subscribed = false;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_window is not { IsOpen: true } || _system == null || !_subscribed)
            return;

        if (_timing.RealTime < _nextRequest)
            return;

        _nextRequest = _timing.RealTime + TimeSpan.FromSeconds(_refreshSeconds);
        _system.RequestReport();
    }
}
