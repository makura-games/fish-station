using System.Net;
using System.Net.Sockets;
using Content.Shared.GameTicking;
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
        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnded);
    }

    /// <summary>
    /// Конец раунда: блоки и окна забываем целиком, иначе память и следы «ферм»
    /// перетекают из раунда в раунд.
    /// </summary>
    private void OnRoundEnded(RoundEndedEvent args)
    {
        _byBlock.Clear();
        _firstSeen.Clear();
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
    /// Ключ адресного блока: /24 для четвёртой версии, /64 для шестой. Разбираем адрес
    /// числами, а не обрезкой строки: «::ffff:1.2.3.4» и скобки вокруг IPv6 при строковой
    /// резке дают мусор, а на нём строится обвинение.
    /// Точный адрес не сохраняем: он меняется у мобильных и провайдерских сетей,
    /// а блок остаётся.
    /// </summary>
    private static string? BlockOf(string endpoint)
    {
        var text = endpoint.AsSpan();

        var close = text.LastIndexOf(']');
        if (close >= 0)
            text = text[1..close];
        else
        {
            var colon = text.LastIndexOf(':');
            if (colon > 0)
                text = text[..colon];
        }

        if (!IPAddress.TryParse(text, out var ip))
            return null;

        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        var bytes = ip.GetAddressBytes();

        if (ip.AddressFamily == AddressFamily.InterNetwork && bytes.Length == 4)
            return $"v4/{bytes[0]}.{bytes[1]}.{bytes[2]}";

        if (ip.AddressFamily == AddressFamily.InterNetworkV6 && bytes.Length == 16)
            return $"v6/{Convert.ToHexString(bytes[..8])}";

        return null;
    }
}
