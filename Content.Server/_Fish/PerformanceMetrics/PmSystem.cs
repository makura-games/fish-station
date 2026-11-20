using Content.Server.Chat.Managers;
using Content.Server._Fish.Rpu;
using Content.Shared._Fish.PerformanceMetrics;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Fish.PerformanceMetrics;

/// <summary>
/// Сбор метрик по игровым сессиям: реестр состояний, пороги срабатываний и реакция на них.
/// Работает только на сервере и ничего не сообщает клиентам.
/// </summary>
public sealed partial class PmSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private RpuSystem _rpu = default!;

    private bool _enabled;
    private bool _karma;
    private bool _disconnect;
    private bool _report;

    private readonly Dictionary<NetUserId, PmSession> _sessions = new();

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("perfmon");

        Subs.CVar(_cfg, PmCVars.Enabled, value => _enabled = value, true);
        Subs.CVar(_cfg, PmCVars.Karma, value => _karma = value, true);
        Subs.CVar(_cfg, PmCVars.Disconnect, value => _disconnect = value, true);
        Subs.CVar(_cfg, PmCVars.Report, value => _report = value, true);

        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        switch (args.NewStatus)
        {
            case SessionStatus.Connected:
                GetSession(args.Session.UserId);
                break;
            case SessionStatus.Disconnected:
                _sessions.Remove(args.Session.UserId);
                break;
        }
    }

    /// <summary>Сбор включён.</summary>
    public bool Enabled => _enabled;

    public PmSession GetSession(NetUserId userId)
    {
        if (!_sessions.TryGetValue(userId, out var session))
        {
            session = new PmSession { UserId = userId };
            _sessions[userId] = session;
        }

        return session;
    }

    public bool TryGetSession(NetUserId userId, out PmSession session)
    {
        return _sessions.TryGetValue(userId, out session!);
    }

    public IEnumerable<PmSession> Sessions => _sessions.Values;

    /// <summary>
    /// Признать выброс метрики. То же основание не учитываем повторно в пределах кулдауна,
    /// а реакция включается только когда независимых оснований набралось достаточно.
    /// </summary>
    public void Note(PmSession session, PmFlag flag, string detail)
    {
        if (!_enabled)
            return;

        var now = _timing.CurTime;

        if (session.FlaggedAt.TryGetValue(flag, out var previous) &&
            now - previous < PmTuning.KarmaCooldown)
        {
            _sawmill.Debug($"{session.UserId}: {flag} again ({detail})");
            return;
        }

        session.Mark(flag, now);

        if (!_players.SessionsDict.TryGetValue(session.UserId, out var player))
            return;

        _sawmill.Info($"{player.Name} ({session.UserId}): metric {flag} — {detail}");

        if (_report && now - session.LastReportAt >= PmTuning.ReportCooldown)
        {
            session.LastReportAt = now;
            // Формулировка намеренно про метрики и нагрузку: вывод о причинах остаётся
            // администратору, наружу он не уходит.
            _chat.SendAdminAnnouncement(
                $"Session metrics out of range for {player.Name}: {Describe(session)}. Worth a look.");
        }

        if (_karma && now - session.LastKarmaAt >= PmTuning.KarmaCooldown)
        {
            session.LastKarmaAt = now;
            _rpu.TryAdjustKarma(player, -PmTuning.KarmaPerFlag);
        }

        if (_disconnect && session.TotalFlags >= PmTuning.FlagsForDisconnect)
            player.Channel.Disconnect("Input rate out of range for this server.");
    }

    /// <summary>Сводка по сессии — для админской команды.</summary>
    public string Describe(PmSession session)
    {
        if (session.FlaggedAt.Count == 0)
            return "no flags";

        var parts = new List<string>(session.FlaggedAt.Count);
        foreach (var (flag, at) in session.FlaggedAt)
            parts.Add($"{flag}@{at.TotalMinutes:0}m");

        return $"{session.TotalFlags} flags: {string.Join(", ", parts)}";
    }
}
