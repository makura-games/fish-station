using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Shared._Fish.Achievements;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Achievements;

/// <summary>
/// Админ-команда для ручных/особых достижений.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed partial class AchievementGrantCommand : IConsoleCommand
{
    [Dependency] private AchievementManager _achievements = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public string Command => "achgrant";
    public string Description => "Выдать достижение игроку (admin).";
    public string Help => "achgrant <player> <achievementId>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Help);
            return;
        }

        var playerName = args[0];
        var achievementId = args[1];

        if (!_players.TryGetSessionByUsername(playerName, out var session))
        {
            shell.WriteError($"Игрок '{playerName}' не найден онлайн.");
            shell.WriteLine("Подсказка: для локального клиента ckey часто выглядит как localhost@Имя (без пробелов).");
            return;
        }

        _ = GrantAsync(shell, session, achievementId);
    }

    private async Task GrantAsync(IConsoleShell shell, ICommonSession session, string achievementId)
    {
        if (!_prototypes.HasIndex<AchievementPrototype>(achievementId))
        {
            shell.WriteError($"Достижение '{achievementId}' не найдено в прототипах.");
            shell.WriteLine("Проверь ID в Resources/Prototypes/_Fish/Achievements/ и что сервер собран с веткой achievements.");
            return;
        }

        if (!_achievements.TryGetState(session, out var states))
        {
            shell.WriteError($"Данные достижений для {session.Name} ещё не загружены — подожди пару секунд после входа и повтори.");
            return;
        }

        if (states.TryGetValue(achievementId, out var existing) && existing.Unlocked)
        {
            shell.WriteError($"У {session.Name} уже есть {achievementId}. Для теста popup выбери другую ачивку.");
            return;
        }

        var ok = await _achievements.TryForceUnlockAsync(session, achievementId);
        shell.WriteLine(ok
            ? $"Выдано {achievementId} → {session.Name}"
            : $"Не удалось выдать {achievementId} (внутренняя ошибка записи).");
    }
}
