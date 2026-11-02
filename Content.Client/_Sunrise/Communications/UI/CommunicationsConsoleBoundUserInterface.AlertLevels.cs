using Content.Shared.Access.Systems;
using Content.Shared.Communications;
using Robust.Client.Player;
// Fish-start: импорты для AlertLevelPrototype
using Content.Shared.AlertLevel;
using Robust.Shared.Prototypes;
// Fish-end

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемому upstream-интерфейсу.
namespace Content.Client.Communications.UI;

public sealed partial class CommunicationsConsoleBoundUserInterface
{
    /*
     * Client-side validation and messages for alert-level controls.
     */

    [Dependency] private IPlayerManager _player = default!;

    private AccessReaderSystem AccessReader => EntMan.System<AccessReaderSystem>();

    // Fish-start: типизированный ProtoId<AlertLevelPrototype> вместо string
    // private void AdditionalAlertLevelSelected(string level, bool enabled)
    private void AdditionalAlertLevelSelected(ProtoId<AlertLevelPrototype> level, bool enabled)
    // Fish-end
    {
        if (!HasAccess())
            return;

        SendMessage(new CommunicationsConsoleSetAdditionalAlertLevelMessage(level, enabled));
    }

    private void AlertStationSelected(NetEntity station)
    {
        if (!HasAccess() || _menu == null)
            return;

        // Ждём подтверждённое состояние выбранной станции, чтобы команда не ушла на предыдущую.
        _menu.DisableAlertLevelControls();
        SendMessage(new CommunicationsConsoleSelectAlertStationMessage(station));
    }

    private bool HasAccess()
    {
        return _player.LocalEntity is { } player && AccessReader.IsAllowed(player, Owner);
    }
}
