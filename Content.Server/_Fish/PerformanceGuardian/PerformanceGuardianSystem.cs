using Content.Server.Administration.Managers;
using Content.Shared._Fish.PerformanceGuardian;
using Content.Shared.Administration;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Fish.PerformanceGuardian;

/// <summary>
/// Фасад: idle-мониторинг, диагностика по инциденту/кнопке, сеть для админов.
/// </summary>
public sealed partial class PerformanceGuardianSystem : EntitySystem
{
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private PgCollectorSystem _collector = default!;

    private readonly HashSet<ICommonSession> _subscribers = new();
    private readonly PgLoadClassifier _classifier = new();

    private PgIdleMonitor _idle = default!;
    private PgDiagnostics _diagnostics = default!;

    private bool _enabled = true;
    private float _sampleInterval = 2f;
    private float _nearbyRange = 16f;
    private int _topLimit = 8;
    private float _diagnoseBudgetMs = 3f;

    private TimeSpan _nextSample;
    private TimeSpan _incidentCooldownUntil;
    private PgMode _mode = PgMode.Idle;
    private int _eventRatePerSec;
    private float _eventsAccum;
    private TimeSpan _eventsWindowStart;
    private float _maxFrameTimeSinceSample;

    private PgReport _report = new();

    public bool CollectorsEnabled => _enabled && _mode != PgMode.Incident;

    public override void Initialize()
    {
        base.Initialize();

        _idle = new PgIdleMonitor(EntityManager, _timing, _players, _physics);
        _diagnostics = new PgDiagnostics(EntityManager, _xform, _physics, _lookup, _players);

        Subs.CVar(_cfg, FishCCVars.PgEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, FishCCVars.PgSampleIntervalSeconds, v => _sampleInterval = Math.Max(1f, v), true);
        Subs.CVar(_cfg, FishCCVars.PgIncidentPressureThreshold, v => _classifier.PressureThreshold = v, true);
        Subs.CVar(_cfg, FishCCVars.PgIncidentAtmosSpike, v => _classifier.AtmosSpikeThreshold = v, true);
        Subs.CVar(_cfg, FishCCVars.PgIncidentPhysicsSpike, v => _classifier.PhysicsSpikeThreshold = v, true);
        Subs.CVar(_cfg, FishCCVars.PgNearbyPlayerRange, v => _nearbyRange = v, true);
        Subs.CVar(_cfg, FishCCVars.PgTopEntityLimit, v => _topLimit = Math.Clamp(v, 1, 16), true);
        Subs.CVar(_cfg, FishCCVars.PgDiagnoseBudgetMs, v => _diagnoseBudgetMs = Math.Max(1f, v), true);
        Subs.CVar(_cfg, FishCCVars.PgConfirmationsRequired, v => _classifier.ConfirmationsRequired = Math.Clamp(v, 1, 10), true);
        Subs.CVar(_cfg, FishCCVars.PgWarmupSamples, v => _classifier.WarmupSamples = Math.Clamp(v, 0, 120), true);

        SubscribeNetworkEvent<PgSubscribeRequest>(OnSubscribe);
        SubscribeNetworkEvent<PgUnsubscribeRequest>(OnUnsubscribe);
        SubscribeNetworkEvent<PgReportRequest>(OnReportRequest);
        SubscribeNetworkEvent<PgDiagnoseRequest>(OnDiagnoseRequest);

        _players.PlayerStatusChanged += OnPlayerStatusChanged;
        RefreshReportBasics(PgLoadSource.Ok);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
        _subscribers.Clear();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Disconnected)
            _subscribers.Remove(e.Session);
    }

    private bool IsDebugAdmin(ICommonSession session) =>
        _admins.HasAdminFlag(session, AdminFlags.Debug);

    private void OnSubscribe(PgSubscribeRequest msg, EntitySessionEventArgs args)
    {
        if (!IsDebugAdmin(args.SenderSession))
            return;

        _subscribers.Add(args.SenderSession);
        RaiseNetworkEvent(new PgReportResponse(CloneReport()), args.SenderSession);
    }

    private void OnUnsubscribe(PgUnsubscribeRequest msg, EntitySessionEventArgs args) =>
        _subscribers.Remove(args.SenderSession);

    private void OnReportRequest(PgReportRequest msg, EntitySessionEventArgs args)
    {
        if (!IsDebugAdmin(args.SenderSession))
            return;

        _subscribers.Add(args.SenderSession);
        RefreshReportBasics(_report.PrimarySource);
        RaiseNetworkEvent(new PgReportResponse(CloneReport()), args.SenderSession);
    }

    private void OnDiagnoseRequest(PgDiagnoseRequest msg, EntitySessionEventArgs args)
    {
        if (!IsDebugAdmin(args.SenderSession))
            return;

        RunDiagnostics(manual: true);
        RaiseNetworkEvent(new PgReportResponse(CloneReport()), args.SenderSession);
    }

    public override void Update(float frameTime)
    {
        if (!_enabled)
            return;

        // Реальный сигнал отставания цикла (не эвристика atmos/physics).
        _maxFrameTimeSinceSample = Math.Max(_maxFrameTimeSinceSample, frameTime);

        var now = _timing.CurTime;
        if (now < _nextSample)
            return;

        _nextSample = now + TimeSpan.FromSeconds(_sampleInterval);
        var maxFt = _maxFrameTimeSinceSample;
        _maxFrameTimeSinceSample = 0f;

        _idle.Sample(maxFt);

        var events = _collector.TakeEventCount();
        if (_eventsWindowStart == TimeSpan.Zero)
            _eventsWindowStart = now;

        _eventsAccum += events;
        var window = (float)(now - _eventsWindowStart).TotalSeconds;
        if (window >= 1f)
        {
            _eventRatePerSec = (int)(_eventsAccum / window);
            _eventsAccum = 0;
            _eventsWindowStart = now;
        }

        var tickPeriod = (float)_timing.TickPeriod.TotalSeconds;
        var shouldDiagnose = _classifier.Observe(
            _idle.AwakeBodies,
            _idle.AtmosActive,
            _eventRatePerSec,
            maxFt > 0f ? maxFt : tickPeriod,
            tickPeriod,
            out var hint);

        if (_idle.EntityCount > 12000 && hint is PgLoadSource.Ok or PgLoadSource.Physics)
            hint = PgLoadSource.Entities;

        RefreshReportBasics(hint);

        if (_mode == PgMode.Incident && now >= _incidentCooldownUntil)
        {
            if (!_classifier.IsAnomalous(_idle.AwakeBodies, _idle.AtmosActive, _eventRatePerSec)
                && _classifier.PressureRatio < _classifier.PressureThreshold * 0.9f)
            {
                _mode = PgMode.Idle;
                RefreshReportBasics(PgLoadSource.Ok);
            }
        }
        else if (_mode == PgMode.Idle && now >= _incidentCooldownUntil && shouldDiagnose)
        {
            RunDiagnostics(manual: false);
        }

        if (_subscribers.Count == 0)
            return;

        var payload = CloneReport();
        foreach (var session in _subscribers)
            RaiseNetworkEvent(new PgReportResponse(payload), session);
    }

    private void RunDiagnostics(bool manual)
    {
        _mode = PgMode.Incident;
        _incidentCooldownUntil = _timing.CurTime + TimeSpan.FromSeconds(20);

        _diagnostics.Run(
            _idle,
            _eventRatePerSec,
            _classifier,
            _diagnoseBudgetMs,
            _nearbyRange,
            _topLimit,
            out var source,
            out var sourceText,
            out var place,
            out var coords,
            out var placeTp,
            out var top,
            out var nearby,
            out var recommendation);

        _report.PrimarySource = source;
        _report.PrimarySourceText = sourceText;
        _report.PlaceName = place;
        _report.CoordinatesText = coords;
        _report.PlaceTeleportTarget = placeTp;
        _report.TopEntities = top;
        _report.NearbyPlayers = nearby;
        _report.Recommendation = recommendation;
        _report.LastIncidentAt = _timing.CurTime;
        _report.LastIncidentSummary = manual
            ? $"Ручная диагностика: {sourceText}"
            : $"Авто-инцидент: {sourceText} на «{place}»";
        _report.DiagnosisAvailable = true;

        RefreshReportBasics(source);
    }

    private void RefreshReportBasics(PgLoadSource hint)
    {
        _report.ServerTime = _timing.CurTime;
        _report.Mode = _mode;
        _report.Tps = _idle.LastTpsEstimate;
        _report.TickMs = _idle.LastMaxFrameTime * 1000f;
        _report.TickBudgetMs = _idle.LastTickBudgetMs;
        _report.EntityCount = _idle.EntityCount;
        _report.GridCount = _idle.GridCount;
        _report.AwakeBodies = _idle.AwakeBodies;
        _report.AtmosActiveTiles = _idle.AtmosActive;
        _report.AtmosHotspots = _idle.AtmosHotspots;
        _report.PlayerCount = _idle.PlayerCount;
        _report.EventRatePerSec = _eventRatePerSec;
        _report.ServerState = _classifier.DescribeState(_mode);

        if (_mode == PgMode.Idle && !_report.DiagnosisAvailable)
        {
            _report.PrimarySource = hint;
            _report.PrimarySourceText = PgLoadClassifier.SourceToRu(hint);
            if (hint == PgLoadSource.Ok)
                _report.Recommendation = "Сервер в норме. Нажмите «Подробная проверка (нагружает сервер)», если лаги всё равно есть.";
        }
    }

    private PgReport CloneReport()
    {
        return new PgReport
        {
            ServerTime = _report.ServerTime,
            Mode = _report.Mode,
            ServerState = _report.ServerState,
            Tps = _report.Tps,
            TickMs = _report.TickMs,
            TickBudgetMs = _report.TickBudgetMs,
            EntityCount = _report.EntityCount,
            GridCount = _report.GridCount,
            AwakeBodies = _report.AwakeBodies,
            AtmosActiveTiles = _report.AtmosActiveTiles,
            AtmosHotspots = _report.AtmosHotspots,
            PlayerCount = _report.PlayerCount,
            EventRatePerSec = _report.EventRatePerSec,
            PrimarySource = _report.PrimarySource,
            PrimarySourceText = _report.PrimarySourceText,
            PlaceName = _report.PlaceName,
            CoordinatesText = _report.CoordinatesText,
            PlaceTeleportTarget = _report.PlaceTeleportTarget,
            TopEntities = new List<PgEntityLoadRow>(_report.TopEntities),
            NearbyPlayers = new List<PgNearbyPlayerRow>(_report.NearbyPlayers),
            LastIncidentSummary = _report.LastIncidentSummary,
            LastIncidentAt = _report.LastIncidentAt,
            Recommendation = _report.Recommendation,
            DiagnosisAvailable = _report.DiagnosisAvailable,
        };
    }

    public void HintOpenWindow(ICommonSession session)
    {
        if (!IsDebugAdmin(session))
            return;

        RaiseNetworkEvent(new PgOpenWindowHint(), session);
    }
}
