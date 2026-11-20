using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Fish.PerformanceMetrics;

/// <summary>
/// Метрики подключений: сколько разных аккаунтов замечено с одного адресного блока
/// за окно наблюдения. Нужно потому, что аппаратный идентификатор и имя учётной записи
/// на стороне клиента полностью под его контролем, а адрес подключения — нет.
/// </summary>
public sealed partial class PmIdentitySystem : EntitySystem
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PmSystem _pm = default!;

    /// <summary>Блок -> аккаунты, которые с него появлялись.</summary>
    private readonly Dictionary<string, HashSet<NetUserId>> _byBlock = new();

    private readonly Dictionary<string, TimeSpan> _firstSeen = new();

    public override void Initialize()
    {
        base.Initialize();

        _players.PlayerStatusChanged += OnStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _players.PlayerStatusChanged -= OnStatusChanged;
    }

    private void OnStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Connected || !_pm.Enabled)
            return;

        var block = BlockOf(args.Session.Channel.RemoteEndPoint.ToString());
        if (block == null)
            return;

        var now = _timing.CurTime;

        if (!_byBlock.TryGetValue(block, out var accounts))
        {
            accounts = new HashSet<NetUserId>();
            _byBlock[block] = accounts;
            _firstSeen[block] = now;
        }
        else if (now - _firstSeen[block] > PmTuning.IdentityWindow)
        {
            // Окно закончилось: начинаем считать заново.
            accounts.Clear();
            _firstSeen[block] = now;
        }

        accounts.Add(args.Session.UserId);

        if (accounts.Count < PmTuning.SameSubnetAccounts)
            return;

        var session = _pm.GetSession(args.Session.UserId);
        if (!session.HasFlag(PmFlag.Identity))
            _pm.Note(session, PmFlag.Identity, $"{accounts.Count} accounts from block {block} within window");
    }

    /// <summary>
    /// Ключ адресного блока: для v4 — первые три октета, для v6 — первые четыре группы.
    /// Точный адрес не сохраняем: он меняется у мобильных и провайдерских сетей,
    /// а блок остаётся.
    /// </summary>
    private static string? BlockOf(string endpoint)
    {
        var colon = endpoint.LastIndexOf(':');
        var address = colon > 0 ? endpoint[..colon] : endpoint;

        if (address.Length == 0)
            return null;

        var dots = address.Split('.');
        if (dots.Length == 4)
            return $"{dots[0]}.{dots[1]}.{dots[2]}";

        // IPv6 или что-то необычное: первые четыре группы.
        var groups = address.Split(':');
        return groups.Length > 1 ? string.Join(":", groups[..Math.Min(4, groups.Length)]) : address;
    }
}
